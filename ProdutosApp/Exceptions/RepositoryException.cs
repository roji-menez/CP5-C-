namespace ProdutosApp.Exceptions;

/// <summary>
/// Exceção lançada pela camada de dados com uma mensagem amigável para o usuário.
/// A exceção original (normalmente uma SqlException) é preservada em InnerException.
/// </summary>
public class RepositoryException : Exception
{
    /// <summary>Operação que falhou (ex.: INSERIR, LISTAR).</summary>
    public string Operacao { get; }

    public RepositoryException(string operacao, string message)
        : base(message)
    {
        Operacao = operacao;
    }

    public RepositoryException(string operacao, string message, Exception innerException)
        : base(message, innerException)
    {
        Operacao = operacao;
    }
}
