using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PreOrderStore.Domain;
using PreOrderStore.Services;

namespace PreOrderStore.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly StoreService _store;
    private readonly CatalogSyncService _sync;
    private readonly AuthService _auth;
    private bool _isRefreshing;
    private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

    public ObservableCollection<Product> Products { get; } = [];
    public ObservableCollection<Product> FilteredProducts { get; } = [];
    public ObservableCollection<CartItem> CartItems { get; } = [];
    public ObservableCollection<PreOrder> PreOrders { get; } = [];
    public ObservableCollection<string> Categories { get; } = ["Все категории"];
    public IReadOnlyList<string> AvailabilityOptions { get; } = ["Любой статус", "Предзаказ открыт", "Скоро"];
    public IReadOnlyList<string> SortOptions { get; } = ["Сначала ближайшие", "По названию", "Сначала дешевле", "Сначала дороже"];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedCategory = "Все категории";
    [ObservableProperty] private string _selectedAvailability = "Любой статус";
    [ObservableProperty] private string _selectedSort = "Сначала ближайшие";
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private string _currentSection = "Каталог";
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _passwordConfirmation = string.Empty;
    [ObservableProperty] private string _notice = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private bool _isRegisterMode;
    [ObservableProperty] private DateTime? _lastSyncUtc;
    [ObservableProperty] private int _cartCount;
    [ObservableProperty] private decimal _cartTotal;

    public string CurrentUserName => _auth.Session.Customer?.DisplayName ?? string.Empty;
    public bool IsAuthenticated => _auth.Session.IsSignedIn;
    public bool IsAuthVisible => !IsAuthenticated;
    public bool IsSignInPanel => IsAuthVisible && !IsRegisterMode;
    public bool IsRegisterPanel => IsAuthVisible && IsRegisterMode;
    public bool IsAppVisible => IsAuthenticated;
    public bool IsCatalogVisible => IsAppVisible && CurrentSection == "Каталог";
    public bool IsCartVisible => IsAppVisible && CurrentSection == "Корзина";
    public bool IsOrdersVisible => IsAppVisible && CurrentSection == "Заказы";
    public bool IsCatalogSelected => CurrentSection == "Каталог";
    public bool IsCartSelected => CurrentSection == "Корзина";
    public bool IsOrdersSelected => CurrentSection == "Заказы";
    public string AuthTitle => IsRegisterMode ? "Создать аккаунт" : "С возвращением";
    public string AuthButtonText => IsRegisterMode ? "Создать аккаунт" : "Войти";
    public string AuthSwitchText => IsRegisterMode ? "Уже есть аккаунт? Войти" : "Впервые здесь? Создать аккаунт";
    public bool HasNotice => !string.IsNullOrWhiteSpace(Notice);
    public bool HasProducts => FilteredProducts.Count > 0;
    public bool HasSelectedProduct => SelectedProduct is not null;
    public bool HasNoProducts => !HasProducts;
    public bool HasNoSelectedProduct => !HasSelectedProduct;
    public bool HasNoOrders => PreOrders.Count == 0;
    public bool HasNoCartItems => CartItems.Count == 0;
    public bool CanCheckout => CartItems.Count > 0 && !IsBusy;
    public string CartTotalText => FormatMoney(CartTotal);
    public string LastSyncText => LastSyncUtc is null
        ? "Даты и цены в каталоге — демонстрационные"
        : $"Демо-витрина обновлена {LastSyncUtc.Value.ToLocalTime():dd.MM.yyyy HH:mm}";
    public string SelectedPriceText => SelectedProduct is null ? string.Empty : FormatMoney(SelectedProduct.Price);
    public string SelectedArrivalText => SelectedProduct is null ? string.Empty : ArrivalLabel(SelectedProduct.ExpectedArrivalDate);
    public string SelectedAvailabilityText => SelectedProduct is null
        ? string.Empty
        : SelectedProduct.IsAvailable ? $"Можно добавить в предзаказ · {SelectedProduct.AvailableQuantity} демо-мест" : "Предзаказ пока закрыт";

    public MainViewModel(StoreService store, CatalogSyncService sync, AuthService auth)
    {
        _store = store;
        _sync = sync;
        _auth = auth;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilters();
    partial void OnSelectedAvailabilityChanged(string value) => ApplyFilters();
    partial void OnSelectedSortChanged(string value) => ApplyFilters();
    partial void OnCurrentSectionChanged(string value)
    {
        NotifySections();
        if (value == "Корзина") _ = LoadCartAsync();
        if (value == "Заказы") _ = LoadOrdersAsync();
    }
    partial void OnIsRegisterModeChanged(bool value)
    {
        NotifyAuthPanels();
        OnPropertyChanged(nameof(AuthTitle));
        OnPropertyChanged(nameof(AuthButtonText));
        OnPropertyChanged(nameof(AuthSwitchText));
    }
    partial void OnNoticeChanged(string value) => OnPropertyChanged(nameof(HasNotice));
    partial void OnCartCountChanged(int value) => OnPropertyChanged(nameof(CanCheckout));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanCheckout));
    partial void OnSelectedProductChanged(Product? value)
    {
        OnPropertyChanged(nameof(SelectedPriceText));
        OnPropertyChanged(nameof(SelectedArrivalText));
        OnPropertyChanged(nameof(SelectedAvailabilityText));
        OnPropertyChanged(nameof(HasSelectedProduct));
        OnPropertyChanged(nameof(HasNoSelectedProduct));
    }

    public async Task InitializeAsync()
    {
        await LoadCatalogAsync();
        if (Products.Count == 0) await RefreshCatalogAsync();
    }

    [RelayCommand]
    private void Navigate(string? section)
    {
        if (section is "Каталог" or "Корзина" or "Заказы") CurrentSection = section;
    }

    [RelayCommand]
    private void SelectProduct(Product? product) => SelectedProduct = product;

    [RelayCommand]
    private void ToggleAuthMode()
    {
        IsRegisterMode = !IsRegisterMode;
        Notice = string.Empty;
        HasError = false;
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (Password != PasswordConfirmation)
        {
            HasError = true;
            Notice = "Пароли не совпадают.";
            return;
        }
        IsBusy = true;
        HasError = false;
        try
        {
            var result = await _auth.RegisterAsync(DisplayName, Email, Password);
            Notice = result.Message;
            HasError = !result.Succeeded;
            if (result.Succeeded) await EnterStoreAsync();
        }
        catch (Exception ex)
        {
            HasError = true;
            Notice = $"Не удалось создать аккаунт: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        IsBusy = true;
        HasError = false;
        try
        {
            var result = await _auth.SignInAsync(Email, Password);
            Notice = result.Message;
            HasError = !result.Succeeded;
            if (result.Succeeded) await EnterStoreAsync();
        }
        catch (Exception ex)
        {
            HasError = true;
            Notice = $"Не удалось выполнить вход: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void SignOut()
    {
        _auth.Session.SignOut();
        Email = string.Empty;
        Password = string.Empty;
        PasswordConfirmation = string.Empty;
        IsRegisterMode = false;
        Notice = "Вы вышли из аккаунта на этом устройстве.";
        NotifyAuthPanels();
        NotifySections();
    }

    [RelayCommand]
    private async Task RefreshCatalogAsync()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;
        IsBusy = true;
        HasError = false;
        Notice = "Обновляем демонстрационную витрину…";
        try
        {
            var count = await _sync.SynchronizeAsync();
            await LoadCatalogAsync();
            LastSyncUtc = DateTime.UtcNow;
            Notice = $"Витрина обновлена · позиций: {count}. Даты и цены примера.";
        }
        catch (Exception ex)
        {
            HasError = true;
            Notice = $"Не удалось обновить каталог: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _isRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task AddToCartAsync(Product? product)
    {
        var customerId = _auth.Session.Customer?.Id;
        if (product is null || customerId is null) return;
        try
        {
            await _store.AddToCartAsync(customerId.Value, product.Id);
            Notice = $"«{product.Name}» добавлен в корзину.";
            await LoadCartAsync();
        }
        catch (PreOrderException ex)
        {
            HasError = true;
            Notice = ex.Message;
        }
        if (SelectedProduct?.Id == product.Id) SelectedProduct = product;
    }

    [RelayCommand]
    private async Task RemoveFromCartAsync(CartItem? item)
    {
        var customerId = _auth.Session.Customer?.Id;
        if (item is null || customerId is null) return;
        try
        {
            await _store.RemoveFromCartAsync(customerId.Value, item.Id);
            await LoadCartAsync();
            Notice = "Корзина обновлена.";
        }
        catch (Exception ex)
        {
            HasError = true;
            Notice = ex.Message;
        }
    }

    [RelayCommand]
    private async Task CheckoutAsync()
    {
        var customerId = _auth.Session.Customer?.Id;
        if (customerId is null) return;
        IsBusy = true;
        HasError = false;
        try
        {
            var orders = await _store.CheckoutAsync(customerId.Value);
            Notice = orders.Count == 1
                ? $"Предзаказ №{orders[0].Id} оформлен. Оплата не производится."
                : $"Оформлено предзаказов: {orders.Count}. Оплата не производится.";
            await LoadCartAsync();
            await LoadOrdersAsync();
            CurrentSection = "Заказы";
        }
        catch (PreOrderException ex)
        {
            HasError = true;
            Notice = ex.Message;
        }
        catch (Exception ex)
        {
            HasError = true;
            Notice = $"Не удалось оформить предзаказ: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CancelOrderAsync(PreOrder? order)
    {
        var customerId = _auth.Session.Customer?.Id;
        if (order is null || customerId is null) return;
        try
        {
            await _store.CancelPreOrderAsync(order.Id, customerId.Value);
            Notice = $"Предзаказ №{order.Id} отменён.";
            await LoadOrdersAsync();
        }
        catch (Exception ex)
        {
            HasError = true;
            Notice = ex.Message;
        }
    }

    private async Task EnterStoreAsync()
    {
        Password = string.Empty;
        PasswordConfirmation = string.Empty;
        NotifyAuthPanels();
        NotifySections();
        OnPropertyChanged(nameof(CurrentUserName));
        await LoadCartAsync();
        CurrentSection = "Каталог";
    }

    private async Task LoadCartAsync()
    {
        var customerId = _auth.Session.Customer?.Id;
        if (customerId is null) return;
        var items = await _store.GetCartItemsAsync(customerId.Value);
        CartItems.Clear();
        foreach (var item in items) CartItems.Add(item);
        CartCount = items.Sum(x => x.Quantity);
        CartTotal = items.Sum(x => x.Quantity * x.Product.Price);
        OnPropertyChanged(nameof(HasNoCartItems));
        OnPropertyChanged(nameof(CartTotalText));
    }

    private async Task LoadOrdersAsync()
    {
        var customerId = _auth.Session.Customer?.Id;
        if (customerId is null) return;
        try
        {
            var orders = await _store.GetPreOrdersAsync(customerId.Value);
            PreOrders.Clear();
            foreach (var order in orders) PreOrders.Add(order);
            OnPropertyChanged(nameof(HasNoOrders));
        }
        catch (Exception ex)
        {
            HasError = true;
            Notice = $"Не удалось прочитать историю заказов: {ex.Message}";
        }
    }

    private async Task LoadCatalogAsync()
    {
        var items = await _store.GetProductsAsync();
        Products.Clear();
        foreach (var item in items) Products.Add(item);
        if (SelectedProduct is not null)
            SelectedProduct = Products.FirstOrDefault(x => x.Id == SelectedProduct.Id);
        var categories = Products.Select(x => x.Category).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToArray();
        Categories.Clear();
        Categories.Add("Все категории");
        foreach (var category in categories) Categories.Add(category);
        if (!Categories.Contains(SelectedCategory)) SelectedCategory = "Все категории";
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        IEnumerable<Product> query = Products;
        if (!string.IsNullOrWhiteSpace(SearchText))
            query = query.Where(x => x.Name.Contains(SearchText.Trim(), StringComparison.CurrentCultureIgnoreCase));
        if (SelectedCategory != "Все категории") query = query.Where(x => x.Category == SelectedCategory);
        if (SelectedAvailability == "Предзаказ открыт") query = query.Where(x => x.IsAvailable);
        else if (SelectedAvailability == "Скоро") query = query.Where(x => !x.IsAvailable);
        query = SelectedSort switch
        {
            "По названию" => query.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase),
            "Сначала дешевле" => query.OrderBy(x => x.Price).ThenBy(x => x.Name),
            "Сначала дороже" => query.OrderByDescending(x => x.Price).ThenBy(x => x.Name),
            _ => query.OrderBy(x => x.ExpectedArrivalDate is null).ThenBy(x => x.ExpectedArrivalDate).ThenBy(x => x.Name)
        };
        FilteredProducts.Clear();
        foreach (var product in query) FilteredProducts.Add(product);
        OnPropertyChanged(nameof(HasProducts));
        OnPropertyChanged(nameof(HasNoProducts));
    }

    private void NotifyAuthPanels()
    {
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(IsAuthVisible));
        OnPropertyChanged(nameof(IsSignInPanel));
        OnPropertyChanged(nameof(IsRegisterPanel));
        OnPropertyChanged(nameof(IsAppVisible));
        OnPropertyChanged(nameof(CurrentUserName));
        NotifySections();
    }

    private void NotifySections()
    {
        OnPropertyChanged(nameof(IsCatalogVisible));
        OnPropertyChanged(nameof(IsCartVisible));
        OnPropertyChanged(nameof(IsOrdersVisible));
        OnPropertyChanged(nameof(IsCatalogSelected));
        OnPropertyChanged(nameof(IsCartSelected));
        OnPropertyChanged(nameof(IsOrdersSelected));
    }

    public static string FormatMoney(decimal value) => value.ToString("N0", RuCulture) + " ₽";
    public static string ArrivalLabel(DateTime? date) => date is null ? "Дата выхода уточняется" : $"Ожидается {date.Value:dd.MM.yyyy}";
}
