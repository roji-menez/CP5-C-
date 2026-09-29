using System.Diagnostics;
using System.IO;
using System.Text;

namespace ProdutosApp.Services;

/// <summary>
/// Log simples em arquivo: logs/operacoes.log (relativo à pasta do executável).
/// Formato: [yyyy-MM-dd HH:mm:ss] [INFO|ERRO] OPERACAO - detalhes
/// Thread-safe (lock) e nunca lança exceção para quem o chama.
/// </summary>
public static class Logger
{
    private static readonly object Trava = new();

    public static string CaminhoArquivo { get; } =
        Path.Combine(AppContext.BaseDirectory, "logs", "operacoes.log");

    public static void Info(string operacao, string detalhes) =>
        Escrever("INFO", operacao, detalhes);

    public static void Erro(string operacao, string detalhes, Exception? excecao = null)
    {
        string texto = excecao is null
            ? detalhes
            : $"{detalhes} | {excecao.GetType().Name}: {excecao.Message}";

        Escrever("ERRO", operacao, texto);
    }

    private static void Escrever(string nivel, string operacao, string detalhes)
    {
        string linha = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{nivel}] {operacao} - {detalhes}";

        try
        {
            lock (Trava)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CaminhoArquivo)!);
                File.AppendAllText(CaminhoArquivo, linha + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            // Falha ao gravar o log não pode derrubar a aplicação.
            Debug.WriteLine($"Falha ao gravar log: {ex.Message}. Linha: {linha}");
        }
    }
}
