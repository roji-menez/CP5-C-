using Microsoft.Extensions.Configuration;

namespace ProdutosApp.Data;

/// <summary>
/// Lê a connection string do arquivo appsettings.json (copiado para a pasta de saída).
/// </summary>
public static class DatabaseConfig
{
    private const string NomeArquivo = "appsettings.json";
    private const string NomeConexao = "DefaultConnection";

    public static string ObterConnectionString()
    {
        IConfiguration configuracao = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(NomeArquivo, optional: false, reloadOnChange: false)
            .Build();

        string? connectionString = configuracao.GetConnectionString(NomeConexao);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"A connection string '{NomeConexao}' não foi encontrada em {NomeArquivo}.");
        }

        return connectionString;
    }
}
