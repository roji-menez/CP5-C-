using ProdutosApp.Models;
using ProdutosApp.Services;

namespace ProdutosApp.Tests;

/// <summary>
/// Testes unitários da validação de dados do Produto (não acessam o banco).
/// </summary>
public class ProdutoValidatorTests
{
    [Theory]
    [InlineData("10,50", 10.50)]
    [InlineData("0", 0)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("R$ 99,90", 99.90)]
    [InlineData("10.5", 10.5)]
    [InlineData("  7  ", 7)]
    public void TentarConverterPreco_ValoresValidos_ConverteEmPtBr(string texto, double esperado)
    {
        bool ok = ProdutoValidator.TentarConverterPreco(texto, out decimal preco, out string erro);

        Assert.True(ok, erro);
        Assert.Equal((decimal)esperado, preco);
        Assert.Empty(erro);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("-0,01")]
    [InlineData("10,555")]
    [InlineData("100000000")]
    public void TentarConverterPreco_ValoresInvalidos_RetornaErro(string? texto)
    {
        bool ok = ProdutoValidator.TentarConverterPreco(texto, out decimal preco, out string erro);

        Assert.False(ok);
        Assert.Equal(0m, preco);
        Assert.NotEmpty(erro);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("15", 15)]
    [InlineData(" 42 ", 42)]
    public void TentarConverterEstoque_ValoresValidos_Converte(string texto, int esperado)
    {
        Assert.True(ProdutoValidator.TentarConverterEstoque(texto, out int estoque, out _));
        Assert.Equal(esperado, estoque);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-5")]
    [InlineData("2,5")]
    [InlineData("dez")]
    public void TentarConverterEstoque_ValoresInvalidos_RetornaErro(string texto)
    {
        Assert.False(ProdutoValidator.TentarConverterEstoque(texto, out _, out string erro));
        Assert.NotEmpty(erro);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData(" 25 ", 25)]
    public void TentarConverterId_ValoresValidos_Converte(string texto, int esperado)
    {
        Assert.True(ProdutoValidator.TentarConverterId(texto, out int id, out _));
        Assert.Equal(esperado, id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("1,5")]
    [InlineData("abc")]
    [InlineData("1; DROP TABLE Produtos")]
    public void TentarConverterId_ValoresInvalidos_RetornaErro(string texto)
    {
        Assert.False(ProdutoValidator.TentarConverterId(texto, out _, out string erro));
        Assert.NotEmpty(erro);
    }

    [Fact]
    public void Validar_ProdutoValido_NaoRetornaErros()
    {
        var produto = new Produto { Nome = "Teclado", Preco = 99.90m, Estoque = 10, Categoria = "Periféricos" };

        Assert.Empty(ProdutoValidator.Validar(produto));
    }

    [Fact]
    public void Validar_CamposObrigatoriosVazios_RetornaErros()
    {
        var produto = new Produto { Nome = "  ", Preco = 1, Estoque = 1, Categoria = "" };

        List<string> erros = ProdutoValidator.Validar(produto);

        Assert.Contains("O nome é obrigatório.", erros);
        Assert.Contains("A categoria é obrigatória.", erros);
    }

    [Fact]
    public void Validar_ValoresNegativos_RetornaErros()
    {
        var produto = new Produto { Nome = "Mouse", Preco = -1, Estoque = -1, Categoria = "Periféricos" };

        List<string> erros = ProdutoValidator.Validar(produto);

        Assert.Contains("O preço não pode ser negativo.", erros);
        Assert.Contains("O estoque não pode ser negativo.", erros);
    }

    [Fact]
    public void Validar_TextosMaioresQueAsColunas_RetornaErros()
    {
        var produto = new Produto
        {
            Nome = new string('A', ProdutoValidator.TamanhoMaximoNome + 1),
            Preco = 1,
            Estoque = 1,
            Categoria = new string('B', ProdutoValidator.TamanhoMaximoCategoria + 1)
        };

        List<string> erros = ProdutoValidator.Validar(produto);

        Assert.Equal(2, erros.Count);
    }

    [Fact]
    public void ValidarFormulario_DadosValidos_MontaProdutoSemEspacos()
    {
        List<string> erros = ProdutoValidator.ValidarFormulario(
            "  Monitor  ", "899,00", "8", " Monitores ", out Produto produto);

        Assert.Empty(erros);
        Assert.Equal("Monitor", produto.Nome);
        Assert.Equal(899.00m, produto.Preco);
        Assert.Equal(8, produto.Estoque);
        Assert.Equal("Monitores", produto.Categoria);
    }

    [Fact]
    public void ValidarFormulario_TudoInvalido_RetornaUmErroPorCampo()
    {
        List<string> erros = ProdutoValidator.ValidarFormulario("", "abc", "-1", "", out _);

        Assert.Equal(4, erros.Count);
    }
}
