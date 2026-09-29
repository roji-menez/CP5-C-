using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using ProdutosApp.Data;
using ProdutosApp.Services;

namespace ProdutosApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ConfigurarCultura();
        DispatcherUnhandledException += TratarExcecaoNaoTratada;

        IProdutoRepository repositorio;
        try
        {
            repositorio = new ProdutoRepository(DatabaseConfig.ObterConnectionString());
        }
        catch (Exception ex)
        {
            Logger.Erro("INICIAR", "Falha ao ler a configuração do banco", ex);
            MessageBox.Show(
                "Não foi possível ler a configuração do banco de dados (appsettings.json).\n\n" + ex.Message,
                "Erro de configuração", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        Logger.Info("INICIAR", "Aplicação iniciada");

        var janela = new MainWindow(repositorio);
        MainWindow = janela;
        janela.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Info("SAIR", "Aplicação encerrada");
        base.OnExit(e);
    }

    /// <summary>
    /// Usa pt-BR em toda a aplicação (vírgula decimal e moeda R$).
    /// </summary>
    private static void ConfigurarCultura()
    {
        var cultura = new CultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        Thread.CurrentThread.CurrentCulture = cultura;
        Thread.CurrentThread.CurrentUICulture = cultura;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(cultura.IetfLanguageTag)));
    }

    /// <summary>
    /// Última linha de defesa: qualquer exceção não capturada é registrada e a aplicação continua aberta.
    /// </summary>
    private void TratarExcecaoNaoTratada(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Erro("GERAL", "Exceção não tratada", e.Exception);
        MessageBox.Show(
            "Ocorreu um erro inesperado. Os detalhes foram registrados no log.",
            "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
