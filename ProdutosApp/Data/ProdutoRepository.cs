using System.Data;
using Microsoft.Data.SqlClient;
using ProdutosApp.Exceptions;
using ProdutosApp.Models;
using ProdutosApp.Services;

namespace ProdutosApp.Data;

/// <summary>
/// Acesso a dados de produtos com ADO.NET puro (Microsoft.Data.SqlClient).
///
/// Regras seguidas em todos os métodos:
///  - SQL sempre parametrizado com SqlParameter e SqlDbType explícito (nunca concatenação/interpolação de valores);
///  - SqlConnection, SqlCommand, SqlDataReader e SqlTransaction dentro de "using";
///  - SqlException convertida em RepositoryException com mensagem amigável (original em InnerException);
///  - toda operação é registrada no log (sucesso e erro).
/// </summary>
public class ProdutoRepository : IProdutoRepository
{
    private readonly string _connectionString;

    /// <summary>Usa a connection string do appsettings.json.</summary>
    public ProdutoRepository()
        : this(DatabaseConfig.ObterConnectionString())
    {
    }

    /// <summary>Permite informar outra connection string (útil para testes).</summary>
    public ProdutoRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A connection string não pode ser vazia.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public int Inserir(Produto produto)
    {
        ArgumentNullException.ThrowIfNull(produto);

        // Escolha para obter o Id gerado: SCOPE_IDENTITY() no MESMO lote do INSERT, devolvido por um
        // parâmetro de saída (@NovoId), o que permite executar o INSERT com ExecuteNonQuery.
        //  - Um SELECT SCOPE_IDENTITY() em um SqlCommand separado NÃO funcionaria: comandos parametrizados
        //    são executados via sp_executesql, cada um em seu próprio escopo, e o SELECT retornaria NULL.
        //  - @@IDENTITY poderia devolver o Id de outra tabela caso exista um trigger.
        //  - OUTPUT INSERTED.Id também funcionaria, mas exigiria ExecuteScalar/ExecuteReader e não é
        //    permitido em tabelas com triggers sem a cláusula INTO.
        const string sql = @"
INSERT INTO dbo.Produtos (Nome, Preco, Estoque, Categoria, Ativo)
VALUES (@Nome, @Preco, @Estoque, @Categoria, 1);
SET @NovoId = CAST(SCOPE_IDENTITY() AS INT);";

        try
        {
            using SqlConnection conexao = AbrirConexao("INSERIR");
            using var comando = new SqlCommand(sql, conexao);

            AdicionarParametrosDoProduto(comando, produto);
            SqlParameter parametroNovoId = comando.Parameters.Add("@NovoId", SqlDbType.Int);
            parametroNovoId.Direction = ParameterDirection.Output;

            comando.ExecuteNonQuery();

            int novoId = (int)parametroNovoId.Value;
            produto.Id = novoId;
            produto.Ativo = true;

            Logger.Info("INSERIR", $"Produto Id {novoId} '{produto.Nome}' inserido");
            return novoId;
        }
        catch (SqlException ex)
        {
            throw CriarExcecao("INSERIR", $"Falha ao inserir o produto '{produto.Nome}'", ex);
        }
    }

    public List<Produto> Listar()
    {
        const string sql = "SELECT Id, Nome, Preco, Estoque, Categoria, Ativo FROM dbo.Produtos WHERE Ativo = 1 ORDER BY Id;";

        try
        {
            using SqlConnection conexao = AbrirConexao("LISTAR");
            using var comando = new SqlCommand(sql, conexao);

            using SqlDataReader reader = comando.ExecuteReader();

            var produtos = new List<Produto>();
            while (reader.Read())
            {
                produtos.Add(MapearProduto(reader));
            }

            Logger.Info("LISTAR", $"{produtos.Count} produto(s) ativo(s) listado(s)");
            return produtos;
        }
        catch (SqlException ex)
        {
            throw CriarExcecao("LISTAR", "Falha ao listar os produtos", ex);
        }
    }

    public Produto? BuscarPorId(int id)
    {
        const string sql = "SELECT Id, Nome, Preco, Estoque, Categoria, Ativo FROM dbo.Produtos WHERE Id = @Id AND Ativo = 1;";

        try
        {
            using SqlConnection conexao = AbrirConexao("BUSCAR");
            using var comando = new SqlCommand(sql, conexao);
            comando.Parameters.Add("@Id", SqlDbType.Int).Value = id;

            using SqlDataReader reader = comando.ExecuteReader();

            if (!reader.Read())
            {
                Logger.Info("BUSCAR", $"Produto Id {id} não encontrado");
                return null;
            }

            Produto produto = MapearProduto(reader);
            Logger.Info("BUSCAR", $"Produto Id {id} '{produto.Nome}' encontrado");
            return produto;
        }
        catch (SqlException ex)
        {
            throw CriarExcecao("BUSCAR", $"Falha ao buscar o produto Id {id}", ex);
        }
    }

    public bool Atualizar(Produto produto)
    {
        ArgumentNullException.ThrowIfNull(produto);

        const string sql = @"
UPDATE dbo.Produtos
   SET Nome = @Nome,
       Preco = @Preco,
       Estoque = @Estoque,
       Categoria = @Categoria
 WHERE Id = @Id
   AND Ativo = 1;";

        try
        {
            using SqlConnection conexao = AbrirConexao("ATUALIZAR");
            using var comando = new SqlCommand(sql, conexao);

            AdicionarParametrosDoProduto(comando, produto);
            comando.Parameters.Add("@Id", SqlDbType.Int).Value = produto.Id;

            int linhasAfetadas = comando.ExecuteNonQuery();
            bool atualizado = linhasAfetadas == 1;

            if (atualizado)
            {
                Logger.Info("ATUALIZAR", $"Produto Id {produto.Id} '{produto.Nome}' atualizado");
            }
            else
            {
                Logger.Info("ATUALIZAR", $"Produto Id {produto.Id} não encontrado; nada foi alterado");
            }

            return atualizado;
        }
        catch (SqlException ex)
        {
            throw CriarExcecao("ATUALIZAR", $"Falha ao atualizar o produto Id {produto.Id}", ex);
        }
    }

    public bool Excluir(int id)
    {
        // Soft delete: o registro permanece no banco, apenas marcado como inativo.
        // A transação garante que a exclusão só é confirmada (Commit) se exatamente 1 linha foi afetada;
        // caso contrário, ou em caso de erro, tudo é desfeito (Rollback). Também deixa o método pronto
        // para incluir outros comandos atômicos no futuro (ex.: gravar um histórico de exclusões).
        const string sql = "UPDATE dbo.Produtos SET Ativo = 0 WHERE Id = @Id AND Ativo = 1;";

        try
        {
            using SqlConnection conexao = AbrirConexao("EXCLUIR");
            using SqlTransaction transacao = conexao.BeginTransaction();
            try
            {
                using var comando = new SqlCommand(sql, conexao, transacao);
                comando.Parameters.Add("@Id", SqlDbType.Int).Value = id;

                int linhasAfetadas = comando.ExecuteNonQuery();

                if (linhasAfetadas == 1)
                {
                    transacao.Commit();
                    Logger.Info("EXCLUIR", $"Produto Id {id} excluído (soft delete) - transação confirmada");
                    return true;
                }

                transacao.Rollback();
                Logger.Info("EXCLUIR", $"Produto Id {id} não encontrado - transação desfeita");
                return false;
            }
            catch (SqlException)
            {
                DesfazerTransacao(transacao, id);
                throw;
            }
        }
        catch (SqlException ex)
        {
            throw CriarExcecao("EXCLUIR", $"Falha ao excluir o produto Id {id}", ex);
        }
    }

    /// <summary>
    /// Mapeamento manual de uma linha do SqlDataReader para um objeto Produto.
    /// </summary>
    private static Produto MapearProduto(SqlDataReader reader)
    {
        int ordinalId = reader.GetOrdinal("Id");
        int ordinalNome = reader.GetOrdinal("Nome");
        int ordinalPreco = reader.GetOrdinal("Preco");
        int ordinalEstoque = reader.GetOrdinal("Estoque");
        int ordinalCategoria = reader.GetOrdinal("Categoria");
        int ordinalAtivo = reader.GetOrdinal("Ativo");

        return new Produto
        {
            Id = reader.GetInt32(ordinalId),
            Nome = reader.GetString(ordinalNome),
            Preco = reader.GetDecimal(ordinalPreco),
            Estoque = reader.GetInt32(ordinalEstoque),
            Categoria = reader.GetString(ordinalCategoria),
            Ativo = reader.GetBoolean(ordinalAtivo)
        };
    }

    /// <summary>
    /// Parâmetros comuns a INSERT e UPDATE, todos com tipo explícito.
    /// Os textos não recebem tamanho fixo de propósito: se o valor for maior que a coluna, o SQL Server
    /// gera o erro 2628 (tratado abaixo) em vez de o SqlClient truncar o texto silenciosamente.
    /// </summary>
    private static void AdicionarParametrosDoProduto(SqlCommand comando, Produto produto)
    {
        comando.Parameters.Add("@Nome", SqlDbType.NVarChar).Value = produto.Nome;

        SqlParameter parametroPreco = comando.Parameters.Add("@Preco", SqlDbType.Decimal);
        parametroPreco.Precision = 10;
        parametroPreco.Scale = 2;
        parametroPreco.Value = produto.Preco;

        comando.Parameters.Add("@Estoque", SqlDbType.Int).Value = produto.Estoque;
        comando.Parameters.Add("@Categoria", SqlDbType.NVarChar).Value = produto.Categoria;
    }

    private static void DesfazerTransacao(SqlTransaction transacao, int id)
    {
        try
        {
            transacao.Rollback();
            Logger.Info("EXCLUIR", $"Rollback executado para o produto Id {id}");
        }
        catch (Exception ex)
        {
            // Se a conexão caiu, o próprio servidor já desfaz a transação.
            Logger.Erro("EXCLUIR", $"Falha ao executar rollback do produto Id {id}", ex);
        }
    }

    /// <summary>
    /// Abre a conexão. Qualquer SqlException neste ponto é uma falha de conexão (servidor fora do ar,
    /// instância do LocalDB inexistente, banco não criado...), cujos números de erro variam muito.
    /// </summary>
    private SqlConnection AbrirConexao(string operacao)
    {
        var conexao = new SqlConnection(_connectionString);
        try
        {
            conexao.Open();
            return conexao;
        }
        catch (SqlException ex)
        {
            conexao.Dispose();
            throw CriarExcecao(operacao, "Falha ao abrir a conexão com o banco", ex, falhaAoConectar: true);
        }
    }

    /// <summary>
    /// Registra o erro no log e converte a SqlException em RepositoryException com mensagem amigável,
    /// de acordo com o número do erro do SQL Server.
    /// </summary>
    private static RepositoryException CriarExcecao(
        string operacao, string contexto, SqlException ex, bool falhaAoConectar = false)
    {
        const string mensagemFalhaConexao =
            "Não foi possível conectar ao banco de dados. Verifique se o SQL Server LocalDB está " +
            "instalado e em execução e se a connection string do appsettings.json está correta.";

        string mensagem = ex.Number switch
        {
            2627 or 2601 =>
                "Já existe um registro com esses dados (valor duplicado).",
            547 =>
                "Os dados violam uma regra do banco de dados: preço e estoque não podem ser negativos.",
            2628 or 8152 =>
                "Um dos textos é maior que o permitido (Nome: até 100 caracteres, Categoria: até 50).",
            8115 =>
                "Um valor numérico está fora do intervalo permitido.",
            -2 =>
                "O banco de dados demorou demais para responder (tempo esgotado). Tente novamente.",
            4060 =>
                "O banco ProdutosDB não foi encontrado. Execute o script database/script.sql.",
            208 =>
                "A tabela Produtos não foi encontrada. Execute o script database/script.sql.",
            18456 =>
                "Acesso negado ao banco de dados. Verifique as credenciais da connection string.",
            -1 or 2 or 53 or 233 or 10053 or 10054 or 10060 or 10061 or 11001 =>
                mensagemFalhaConexao,
            _ when falhaAoConectar =>
                mensagemFalhaConexao,
            _ =>
                "Ocorreu um erro inesperado no banco de dados. Os detalhes foram registrados no log."
        };

        Logger.Erro(operacao, $"{contexto} (erro SQL {ex.Number})", ex);
        return new RepositoryException(operacao, mensagem, ex);
    }
}
