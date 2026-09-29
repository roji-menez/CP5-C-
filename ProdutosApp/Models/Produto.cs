namespace ProdutosApp.Models;

/// <summary>
/// Representa um registro da tabela dbo.Produtos.
/// </summary>
public class Produto
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public decimal Preco { get; set; }

    public int Estoque { get; set; }

    public string Categoria { get; set; } = string.Empty;

    /// <summary>
    /// false indica que o produto foi excluído logicamente (soft delete).
    /// </summary>
    public bool Ativo { get; set; } = true;

    public override string ToString() => $"#{Id} {Nome}";
}
