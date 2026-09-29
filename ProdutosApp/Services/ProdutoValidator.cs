using System.Globalization;
using ProdutosApp.Models;

namespace ProdutosApp.Services;

/// <summary>
/// Validação dos dados digitados antes de chamar o repositório.
/// Fica fora da janela para poder ser testada com xUnit.
/// </summary>
public static class ProdutoValidator
{
    public const int TamanhoMaximoNome = 100;
    public const int TamanhoMaximoCategoria = 50;

    /// <summary>Maior valor que cabe em DECIMAL(10,2).</summary>
    public const decimal PrecoMaximo = 99_999_999.99m;

    private static readonly CultureInfo CulturaBrasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Valida o formulário inteiro e monta o Produto. Retorna a lista de erros (vazia se tudo estiver certo).
    /// </summary>
    public static List<string> ValidarFormulario(
        string? nome, string? preco, string? estoque, string? categoria, out Produto produto)
    {
        var erros = new List<string>();

        if (!TentarConverterPreco(preco, out decimal valorPreco, out string erroPreco))
        {
            erros.Add(erroPreco);
        }

        if (!TentarConverterEstoque(estoque, out int valorEstoque, out string erroEstoque))
        {
            erros.Add(erroEstoque);
        }

        produto = new Produto
        {
            Nome = (nome ?? string.Empty).Trim(),
            Preco = valorPreco,
            Estoque = valorEstoque,
            Categoria = (categoria ?? string.Empty).Trim()
        };

        erros.AddRange(ValidarTextos(produto));
        return erros;
    }

    /// <summary>
    /// Valida um Produto já montado (regras equivalentes às constraints da tabela).
    /// </summary>
    public static List<string> Validar(Produto produto)
    {
        ArgumentNullException.ThrowIfNull(produto);

        var erros = ValidarTextos(produto);

        if (produto.Preco < 0)
        {
            erros.Add("O preço não pode ser negativo.");
        }
        else if (produto.Preco > PrecoMaximo)
        {
            erros.Add($"O preço não pode ser maior que {PrecoMaximo.ToString("N2", CulturaBrasil)}.");
        }
        else if (decimal.Round(produto.Preco, 2) != produto.Preco)
        {
            erros.Add("O preço deve ter no máximo 2 casas decimais.");
        }

        if (produto.Estoque < 0)
        {
            erros.Add("O estoque não pode ser negativo.");
        }

        return erros;
    }

    /// <summary>
    /// Converte o preço no padrão brasileiro. Aceita "10,50", "1.234,56", "R$ 10,50" e também "10.50".
    /// </summary>
    public static bool TentarConverterPreco(string? texto, out decimal preco, out string erro)
    {
        preco = 0;
        erro = string.Empty;

        if (string.IsNullOrWhiteSpace(texto))
        {
            erro = "O preço é obrigatório.";
            return false;
        }

        string normalizado = texto.Replace("R$", string.Empty).Trim();

        // Sem vírgula e com um único ponto ("10.50"): o ponto é tratado como separador decimal.
        if (!normalizado.Contains(',') && normalizado.Count(c => c == '.') == 1)
        {
            normalizado = normalizado.Replace('.', ',');
        }

        if (!decimal.TryParse(normalizado, NumberStyles.Number, CulturaBrasil, out decimal valor))
        {
            erro = "Preço inválido. Informe um número, por exemplo: 10,50.";
            return false;
        }

        if (valor < 0)
        {
            erro = "O preço não pode ser negativo.";
            return false;
        }

        if (valor > PrecoMaximo)
        {
            erro = $"O preço não pode ser maior que {PrecoMaximo.ToString("N2", CulturaBrasil)}.";
            return false;
        }

        if (decimal.Round(valor, 2) != valor)
        {
            erro = "O preço deve ter no máximo 2 casas decimais.";
            return false;
        }

        preco = valor;
        return true;
    }

    public static bool TentarConverterEstoque(string? texto, out int estoque, out string erro)
    {
        estoque = 0;
        erro = string.Empty;

        if (string.IsNullOrWhiteSpace(texto))
        {
            erro = "O estoque é obrigatório.";
            return false;
        }

        if (!int.TryParse(texto.Trim(), NumberStyles.Integer, CulturaBrasil, out int valor))
        {
            erro = "Estoque inválido. Informe um número inteiro, por exemplo: 10.";
            return false;
        }

        if (valor < 0)
        {
            erro = "O estoque não pode ser negativo.";
            return false;
        }

        estoque = valor;
        return true;
    }

    public static bool TentarConverterId(string? texto, out int id, out string erro)
    {
        id = 0;
        erro = string.Empty;

        if (string.IsNullOrWhiteSpace(texto))
        {
            erro = "Informe o Id do produto (ou selecione um produto na lista).";
            return false;
        }

        if (!int.TryParse(texto.Trim(), NumberStyles.None, CulturaBrasil, out int valor) || valor <= 0)
        {
            erro = "Id inválido. Informe um número inteiro maior que zero.";
            return false;
        }

        id = valor;
        return true;
    }

    private static List<string> ValidarTextos(Produto produto)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(produto.Nome))
        {
            erros.Add("O nome é obrigatório.");
        }
        else if (produto.Nome.Trim().Length > TamanhoMaximoNome)
        {
            erros.Add($"O nome deve ter no máximo {TamanhoMaximoNome} caracteres.");
        }

        if (string.IsNullOrWhiteSpace(produto.Categoria))
        {
            erros.Add("A categoria é obrigatória.");
        }
        else if (produto.Categoria.Trim().Length > TamanhoMaximoCategoria)
        {
            erros.Add($"A categoria deve ter no máximo {TamanhoMaximoCategoria} caracteres.");
        }

        return erros;
    }
}
