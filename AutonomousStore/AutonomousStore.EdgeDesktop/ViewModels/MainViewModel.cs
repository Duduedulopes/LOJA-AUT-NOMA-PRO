using System.Collections.ObjectModel;
using AutonomousStore.EdgeDesktop.Models;
using AutonomousStore.EdgeDesktop.Services;
using AutonomousStore.Hardware.Interfaces;
using AutonomousStore.Hardware.Mocks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutonomousStore.EdgeDesktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IProductApiService _productApiService;
    private readonly ICartService _cartService;
    private readonly ISessionApiService _sessionApiService;
    private readonly IRfidReader _rfidReader;
    private readonly MockRfidReader _mockRfidReader;

    public ObservableCollection<ProductDto> Products { get; } = [];
    public ObservableCollection<CartItem> CartItems { get; } = [];
    public ObservableCollection<SessionItemDto> SessionItems { get; } = [];

    [ObservableProperty]
    private string _statusMessage = "Pronto.";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private decimal _cartTotal;

    [ObservableProperty]
    private int _cartItemCount;

    [ObservableProperty]
    private string _sessionIdInput = "";

    [ObservableProperty]
    private string _rfidTagInput = "";

    [ObservableProperty]
    private SessionDto? _currentSession;

    public MainViewModel(
        IProductApiService productApiService,
        ICartService cartService,
        ISessionApiService sessionApiService,
        IRfidReader rfidReader,
        MockRfidReader mockRfidReader)
    {
        _productApiService = productApiService;
        _cartService = cartService;
        _sessionApiService = sessionApiService;
        _rfidReader = rfidReader;
        _mockRfidReader = mockRfidReader;

        _rfidReader.TagRead += OnRfidTagRead;
        _rfidReader.Start();
    }

    [RelayCommand]
    private async Task LoadProductsAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            StatusMessage = "Carregando produtos...";

            var products = await _productApiService.GetAllAsync();

            Products.Clear();
            foreach (var product in products)
                Products.Add(product);

            StatusMessage = $"{products.Count} produto(s) carregado(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao carregar produtos: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddToCart(ProductDto? product)
    {
        if (product is null)
            return;

        _cartService.AddProduct(product);
        RefreshCart();
        StatusMessage = $"{product.Name} adicionado ao carrinho.";
    }

    [RelayCommand]
    private void RemoveFromCart(CartItem? item)
    {
        if (item is null)
            return;

        _cartService.RemoveProduct(item.ProductId);
        RefreshCart();
        StatusMessage = $"{item.Name} removido do carrinho.";
    }

    [RelayCommand]
    private void ClearCart()
    {
        _cartService.Clear();
        RefreshCart();
        StatusMessage = "Carrinho limpo.";
    }

    /// <summary>
    /// Carrega a sess�o de compra do cliente pelo Id � em produ��o isso seria vinculado
    /// automaticamente (ex: pelo QR code lido na porta); por enquanto, pra testar, o
    /// operador cola o Id da sess�o manualmente (pode pegar no Swagger ou no app do cliente).
    /// </summary>
    [RelayCommand]
    private async Task LoadSessionAsync()
    {
        if (!Guid.TryParse(SessionIdInput, out var sessionId))
        {
            StatusMessage = "Id de sess�o inv�lido � cole um GUID v�lido.";
            return;
        }

        try
        {
            IsBusy = true;
            var session = await _sessionApiService.GetByIdAsync(sessionId);

            if (session is null)
            {
                StatusMessage = "Sess�o n�o encontrada.";
                CurrentSession = null;
                SessionItems.Clear();
                return;
            }

            CurrentSession = session;
            RefreshSessionItems();
            StatusMessage = $"Sess�o carregada � status: {session.Status}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao carregar sess�o: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Bot�o de teste: finge que o leitor RFID acabou de ler a tag digitada.</summary>
    [RelayCommand]
    private void SimulateRfidRead()
    {
        if (string.IsNullOrWhiteSpace(RfidTagInput))
        {
            StatusMessage = "Digite uma tag pra simular a leitura.";
            return;
        }

        _mockRfidReader.SimulateRead(RfidTagInput);
    }

    /// <summary>Chamado automaticamente sempre que o leitor (simulado ou real) detecta uma tag.</summary>
    private async void OnRfidTagRead(object? sender, RfidTagReadEventArgs e)
    {
        if (CurrentSession is null)
        {
            StatusMessage = "Carregue a sess�o do cliente antes de ler uma tag.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = $"Tag lida: {e.Tag} � registrando...";

            var updatedSession = await _sessionApiService.AddItemByRfidAsync(CurrentSession.Id, e.Tag);

            CurrentSession = updatedSession;
            RefreshSessionItems();
            StatusMessage = $"Tag {e.Tag} processada com sucesso.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao processar tag {e.Tag}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshCart()
    {
        CartItems.Clear();
        foreach (var item in _cartService.Items)
            CartItems.Add(item);

        CartTotal = _cartService.Total;
        CartItemCount = _cartService.ItemCount;
    }

    private void RefreshSessionItems()
    {
        SessionItems.Clear();

        if (CurrentSession is null)
            return;

        foreach (var item in CurrentSession.Items)
            SessionItems.Add(item);
    }
}
