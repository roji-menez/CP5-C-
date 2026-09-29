using Microsoft.Data.SqlClient;
using ProdutosApp.Data;
using ProdutosApp.Exceptions;
using ProdutosApp.Models;

namespace ProdutosApp.Tests;

/// <summary>
/// Testes de integração do ProdutoRepository contra o SQL Server LocalDB.
/// Todo registro criado aqui é removido fisicamente no Dispose (executado após cada teste).
/// </summary>
public class ProdutoRepositoryTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private const string PrefixoTeste = "[TESTE] ";

    private readonly DatabaseFixture _banco;
    private readonly ProdutoRepository _repositorio;
    private readonly List<int> _idsCriados = new();

    public ProdutoRepositoryTests(DatabaseFixture banco)
    {
        _banco = banco;
        _repositorio = new ProdutoRepository(banco.ConnectionString);
    }

    public void Dispose()
    {
        foreach (int id in _idsCriados)
        {
            _banco.RemoverFisicamente(id);
        }
    }

    [Fact]
    public void CicloCompleto_InserirBuscarAtualizarExcluir()
    {
        // Inserir
        var produto = NovoProduto("Cabo HDMI", 29.90m, 100, "Acessórios");
        int id = InserirRegistrando(produto);
        Assert.True(id > 0);

        // BuscarPorId
        Produto? encontrado = _repositorio.BuscarPorId(id);
        Assert.NotNull(encontrado);
        Assert.Equal(produto.Nome, encontrado!.Nome);
        Assert.Equal(29.90m, encontrado.Preco);
        Assert.Equal(100, encontrado.Estoque);
        Assert.Equal("Acessórios", encontrado.Categoria);
        Assert.True(encontrado.Ativo);

        // Atualizar
        encontrado.Nome = PrefixoTeste + "Cabo HDMI 2.1";
        encontrado.Preco = 49.99m;
        encontrado.Estoque = 80;
        Assert.True(_repositorio.Atualizar(encontrado));

        Produto? atualizado = _repositorio.BuscarPorId(id);
        Assert.Equal(PrefixoTeste + "Cabo HDMI 2.1", atualizado!.Nome);
        Assert.Equal(49.99m, atualizado.Preco);
        Assert.Equal(80, atualizado.Estoque);
        Assert.Contains(_repositorio.Listar(), p => p.Id == id);

        // Excluir (soft delete): some da listagem e da busca, mas continua no banco com Ativo = 0
        Assert.True(_repositorio.Excluir(id));
        Assert.DoesNotContain(_repositorio.Listar(), p => p.Id == id);
        Assert.Null(_repositorio.BuscarPorId(id));
        Assert.False(_banco.LerAtivo(id));

        // Excluir de novo não afeta nenhuma linha (rollback) e retorna false
        Assert.False(_repositorio.Excluir(id));
    }

    [Fact]
    public void Inserir_TentativaDeSqlInjectionNoNome_EhGravadaComoTextoComum()
    {
        const string nomeMalicioso = "'; DROP TABLE Produtos; --";
        var produto = new Produto { Nome = nomeMalicioso, Preco = 1m, Estoque = 1, Categoria = "Teste" };

        int id = InserirRegistrando(produto);

        Produto? gravado = _repositorio.BuscarPorId(id);
        Assert.NotNull(gravado);
        Assert.Equal(nomeMalicioso, gravado!.Nome);
        Assert.True(_banco.TabelaProdutosExiste());
        Assert.Contains(_repositorio.Listar(), p => p.Id == id);
    }

    [Fact]
    public void Atualizar_IdInexistente_RetornaFalse()
    {
        var produto = NovoProduto("Inexistente", 1m, 1, "Teste");
        produto.Id = int.MaxValue;

        Assert.False(_repositorio.Atualizar(produto));
    }

    [Fact]
    public void Excluir_IdInexistente_RetornaFalse()
    {
        Assert.False(_repositorio.Excluir(int.MaxValue));
    }

    [Fact]
    public void BuscarPorId_IdInexistente_RetornaNull()
    {
        Assert.Null(_repositorio.BuscarPorId(int.MaxValue));
    }

    [Fact]
    public void Inserir_PrecoNegativo_ViolaCheckERelancaRepositoryException()
    {
        var produto = NovoProduto("Preço negativo", -10m, 1, "Teste");

        var ex = Assert.Throws<RepositoryException>(() => InserirRegistrando(produto));

        var sqlEx = Assert.IsType<SqlException>(ex.InnerException);
        Assert.Equal(547, sqlEx.Number);
        Assert.Equal("INSERIR", ex.Operacao);
        Assert.Contains("negativo", ex.Message);
    }

    [Fact]
    public void Inserir_NomeMaiorQueAColuna_RelancaRepositoryException()
    {
        var produto = NovoProduto(new string('X', 150), 1m, 1, "Teste");

        var ex = Assert.Throws<RepositoryException>(() => InserirRegistrando(produto));

        Assert.IsType<SqlException>(ex.InnerException);
        Assert.Contains("100 caracteres", ex.Message);
    }

    [Fact]
    public void Listar_BancoInacessivel_RelancaRepositoryExceptionComMensagemAmigavel()
    {
        var repositorioSemBanco = new ProdutoRepository(
            @"Server=(localdb)\InstanciaQueNaoExiste;Database=ProdutosDB;Trusted_Connection=True;" +
            "TrustServerCertificate=True;Connect Timeout=5;");

        var ex = Assert.Throws<RepositoryException>(() => repositorioSemBanco.Listar());

        Assert.IsType<SqlException>(ex.InnerException);
        Assert.Contains("conectar", ex.Message);
    }

    private Produto NovoProduto(string nome, decimal preco, int estoque, string categoria) =>
        new() { Nome = PrefixoTeste + nome, Preco = preco, Estoque = estoque, Categoria = categoria };

    private int InserirRegistrando(Produto produto)
    {
        int id = _repositorio.Inserir(produto);
        _idsCriados.Add(id);
        return id;
    }
}
