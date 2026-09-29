using ProdutosApp.Models;

namespace ProdutosApp.Data;

/// <summary>
/// Contrato de acesso a dados de produtos. A interface gráfica conversa apenas com este contrato,
/// sem conhecer SQL, SqlConnection ou SqlCommand.
/// </summary>
public interface IProdutoRepository
{
    /// <summary>Insere o produto e retorna o Id gerado pelo banco.</summary>
    int Inserir(Produto produto);

    /// <summary>Lista apenas os produtos ativos (Ativo = 1).</summary>
    List<Produto> Listar();

    /// <summary>Busca um produto ativo pelo Id. Retorna null se não encontrado.</summary>
    Produto? BuscarPorId(int id);

    /// <summary>Atualiza um produto ativo. Retorna true se exatamente 1 linha foi afetada.</summary>
    bool Atualizar(Produto produto);

    /// <summary>Exclusão lógica (Ativo = 0) dentro de uma transação. Retorna true se o produto foi excluído.</summary>
    bool Excluir(int id);
}
