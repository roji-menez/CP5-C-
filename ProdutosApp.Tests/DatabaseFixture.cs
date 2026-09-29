using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using ProdutosApp.Data;

namespace ProdutosApp.Tests;

/// <summary>
/// Preparação compartilhada dos testes de integração: executa o database/script.sql
/// (idempotente) para garantir que o banco e a tabela existam no LocalDB.
/// Contém também utilitários de SQL usados SOMENTE pelos testes (remoção física dos dados de teste).
/// </summary>
public class DatabaseFixture
{
    public string ConnectionString { get; }

    public DatabaseFixture()
    {
        ConnectionString = DatabaseConfig.ObterConnectionString();
        ExecutarScriptDoBanco();
    }

    /// <summary>Remove fisicamente (DELETE) um registro criado pelos testes.</summary>
    public void RemoverFisicamente(int id)
    {
        using var conexao = new SqlConnection(ConnectionString);
        using var comando = new SqlCommand("DELETE FROM dbo.Produtos WHERE Id = @Id;", conexao);
        comando.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        conexao.Open();
        comando.ExecuteNonQuery();
    }

    /// <summary>Verifica se a tabela dbo.Produtos existe.</summary>
    public bool TabelaProdutosExiste()
    {
        using var conexao = new SqlConnection(ConnectionString);
        using var comando = new SqlCommand("SELECT OBJECT_ID(N'dbo.Produtos', N'U');", conexao);
        conexao.Open();
        return comando.ExecuteScalar() is not (null or DBNull);
    }

    /// <summary>Lê o valor de Ativo diretamente do banco (inclusive de registros excluídos).</summary>
    public bool? LerAtivo(int id)
    {
        using var conexao = new SqlConnection(ConnectionString);
        using var comando = new SqlCommand("SELECT Ativo FROM dbo.Produtos WHERE Id = @Id;", conexao);
        comando.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        conexao.Open();
        return comando.ExecuteScalar() is bool ativo ? ativo : null;
    }

    private void ExecutarScriptDoBanco()
    {
        string caminhoScript = Path.Combine(AppContext.BaseDirectory, "script.sql");
        string script = File.ReadAllText(caminhoScript);

        // O script cria o banco, então a conexão inicial precisa ser no "master".
        var builder = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" };

        using var conexao = new SqlConnection(builder.ConnectionString);
        conexao.Open();

        // "GO" não é comando SQL, é um separador de lotes do sqlcmd/SSMS: cada lote é executado separadamente.
        string[] lotes = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        foreach (string lote in lotes.Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            using var comando = new SqlCommand(lote, conexao) { CommandTimeout = 120 };
            comando.ExecuteNonQuery();
        }
    }
}
