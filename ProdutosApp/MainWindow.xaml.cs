using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProdutosApp.Data;
using ProdutosApp.Exceptions;
using ProdutosApp.Models;
using ProdutosApp.Services;

namespace ProdutosApp;

/// <summary>
/// Tela única do sistema. Não contém SQL nem acessa SqlConnection/SqlCommand:
/// toda a persistência passa por IProdutoRepository.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly string[] CategoriasSugeridas =
    {
        "Acessórios", "Armazenamento", "Áudio", "Informática", "Monitores", "Periféricos"
    };

    private static readonly Brush FundoSucesso = new SolidColorBrush(Color.FromRgb(0xE3, 0xF6, 0xE8));
    private static readonly Brush FundoAviso = new SolidColorBrush(Color.FromRgb(0xFF, 0xF4, 0xD6));
    private static readonly Brush FundoErro = new SolidColorBrush(Color.FromRgb(0xFD, 0xE2, 0xE1));

    private readonly IProdutoRepository _repositorio;

    public MainWindow(IProdutoRepository repositorio)
    {
        InitializeComponent();

        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        cmbCategoria.ItemsSource = CategoriasSugeridas;

        Loaded += (_, _) => Executar("LISTAR", Listar);
    }

    // ----------------------------------------------------------------- Eventos dos botões (menu)

    private void BtnInserir_Click(object sender, RoutedEventArgs e) => Executar("INSERIR", Inserir);

    private void BtnListar_Click(object sender, RoutedEventArgs e) => Executar("LISTAR", Listar);

    private void BtnBuscar_Click(object sender, RoutedEventArgs e) => Executar("BUSCAR", Buscar);

    private void BtnAtualizar_Click(object sender, RoutedEventArgs e) => Executar("ATUALIZAR", Atualizar);

    private void BtnExcluir_Click(object sender, RoutedEventArgs e) => Executar("EXCLUIR", Excluir);

    private void BtnLimpar_Click(object sender, RoutedEventArgs e)
    {
        LimparFormulario();
        MostrarStatus("Formulário limpo. Digite um Id para buscar ou preencha os campos para inserir.", FundoSucesso);
    }

    private void BtnSair_Click(object sender, RoutedEventArgs e) => Close();

    private void DgProdutos_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (dgProdutos.SelectedItem is Produto produto)
        {
            PreencherFormulario(produto);
            MostrarStatus($"Produto Id {produto.Id} selecionado para edição.", FundoSucesso);
        }
    }

    // ----------------------------------------------------------------- Operações

    private void Inserir()
    {
        if (!TentarLerProduto(out Produto produto))
        {
            return;
        }

        int novoId = _repositorio.Inserir(produto);

        CarregarLista();
        SelecionarNaLista(novoId);
        MostrarSucesso($"Produto '{produto.Nome}' inserido com o Id {novoId}.", exibirMensagem: true);
    }

    private void Listar()
    {
        int quantidade = CarregarLista();
        MostrarSucesso($"{quantidade} produto(s) listado(s).");
    }

    private void Buscar()
    {
        if (!TentarLerId(out int id))
        {
            return;
        }

        Produto? produto = _repositorio.BuscarPorId(id);
        if (produto is null)
        {
            MostrarAviso($"Nenhum produto ativo encontrado com o Id {id}.");
            return;
        }

        PreencherFormulario(produto);
        SelecionarNaLista(produto.Id);
        MostrarSucesso($"Produto Id {id} encontrado: {produto.Nome}.");
    }

    private void Atualizar()
    {
        if (!TentarLerId(out int id) || !TentarLerProduto(out Produto produto))
        {
            return;
        }

        produto.Id = id;

        if (!_repositorio.Atualizar(produto))
        {
            MostrarAviso($"Nenhum produto ativo encontrado com o Id {id}. Nada foi atualizado.");
            return;
        }

        CarregarLista();
        SelecionarNaLista(id);
        MostrarSucesso($"Produto Id {id} atualizado com sucesso.", exibirMensagem: true);
    }

    private void Excluir()
    {
        if (!TentarLerId(out int id))
        {
            return;
        }

        Produto? produto = _repositorio.BuscarPorId(id);
        if (produto is null)
        {
            MostrarAviso($"Nenhum produto ativo encontrado com o Id {id}.");
            return;
        }

        MessageBoxResult resposta = MessageBox.Show(
            $"Deseja realmente excluir o produto '{produto.Nome}' (Id {id})?",
            "Confirmar exclusão", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

        if (resposta != MessageBoxResult.Yes)
        {
            MostrarStatus("Exclusão cancelada.", FundoAviso);
            return;
        }

        if (!_repositorio.Excluir(id))
        {
            MostrarAviso($"O produto Id {id} não pôde ser excluído (talvez já tenha sido removido).");
            return;
        }

        LimparFormulario();
        CarregarLista();
        MostrarSucesso($"Produto '{produto.Nome}' (Id {id}) excluído com sucesso.", exibirMensagem: true);
    }

    /// <summary>
    /// Executa uma operação tratando os erros: RepositoryException mostra a mensagem amigável
    /// (o repositório já registrou o log); qualquer outra exceção mostra mensagem genérica e é registrada.
    /// </summary>
    private void Executar(string operacao, Action acao)
    {
        try
        {
            acao();
        }
        catch (RepositoryException ex)
        {
            MostrarErro(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.Erro(operacao, "Erro inesperado na interface", ex);
            MostrarErro("Ocorreu um erro inesperado. Os detalhes foram registrados no log.");
        }
    }

    // ----------------------------------------------------------------- Formulário e validação

    private bool TentarLerProduto(out Produto produto)
    {
        List<string> erros = ProdutoValidator.ValidarFormulario(
            txtNome.Text, txtPreco.Text, txtEstoque.Text, cmbCategoria.Text, out produto);

        if (erros.Count == 0)
        {
            return true;
        }

        MostrarAviso("Corrija os campos abaixo:\n\n• " + string.Join("\n• ", erros));
        return false;
    }

    private bool TentarLerId(out int id)
    {
        if (ProdutoValidator.TentarConverterId(txtId.Text, out id, out string erro))
        {
            return true;
        }

        MostrarAviso(erro);
        txtId.Focus();
        return false;
    }

    private int CarregarLista()
    {
        List<Produto> produtos = _repositorio.Listar();
        dgProdutos.ItemsSource = produtos;
        return produtos.Count;
    }

    private void SelecionarNaLista(int id)
    {
        Produto? item = dgProdutos.Items.OfType<Produto>().FirstOrDefault(p => p.Id == id);
        if (item is not null)
        {
            dgProdutos.SelectedItem = item;
            dgProdutos.ScrollIntoView(item);
        }
    }

    private void PreencherFormulario(Produto produto)
    {
        txtId.Text = produto.Id.ToString();
        txtId.IsReadOnly = true; // Id bloqueado durante a edição
        txtNome.Text = produto.Nome;
        txtPreco.Text = produto.Preco.ToString("N2");
        txtEstoque.Text = produto.Estoque.ToString();
        cmbCategoria.Text = produto.Categoria;
    }

    private void LimparFormulario()
    {
        dgProdutos.SelectedItem = null;
        txtId.IsReadOnly = false;
        txtId.Clear();
        txtNome.Clear();
        txtPreco.Clear();
        txtEstoque.Clear();
        cmbCategoria.Text = string.Empty;
        txtNome.Focus();
    }

    // ----------------------------------------------------------------- Mensagens

    private void MostrarSucesso(string mensagem, bool exibirMensagem = false)
    {
        MostrarStatus(mensagem, FundoSucesso);
        if (exibirMensagem)
        {
            MessageBox.Show(mensagem, "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void MostrarAviso(string mensagem)
    {
        MostrarStatus(mensagem.Replace("\n\n• ", " ").Replace("\n• ", " "), FundoAviso);
        MessageBox.Show(mensagem, "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void MostrarErro(string mensagem)
    {
        MostrarStatus(mensagem, FundoErro);
        MessageBox.Show(mensagem, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void MostrarStatus(string mensagem, Brush fundo)
    {
        txtStatus.Text = mensagem;
        brdStatus.Background = fundo;
    }
}
