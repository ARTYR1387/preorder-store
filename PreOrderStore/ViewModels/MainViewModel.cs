using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Styling;
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
    private bool _pendingAuthCheckout;
    private bool _showAuth;
    private string _authReturnSection = "Каталог";
    private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

    public ObservableCollection<Product> Products { get; } = [];
    public ObservableCollection<Product> FilteredProducts { get; } = [];
    public ObservableCollection<CartItem> CartItems { get; } = [];
    public ObservableCollection<PreOrder> PreOrders { get; } = [];
    public ObservableCollection<string> Categories { get; } = ["Все категории"];
    public IReadOnlyList<string> AvailabilityOptions { get; } = ["Любой статус", "Предзаказ доступен", "Нет в наличии"];
    public IReadOnlyList<string> SortOptions { get; } = ["По популярному каталогу", "По названию", "Сначала дешевле", "Сначала дороже"];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedCategory = "Все категории";
    [ObservableProperty] private string _selectedAvailability = "Любой статус";
    [ObservableProperty] private string _selectedSort = "По популярному каталогу";
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
    [ObservableProperty] private bool _isDarkTheme;
    [ObservableProperty] private DateTime? _lastSyncUtc;
    [ObservableProperty] private int _cartCount;
    [ObservableProperty] private decimal _cartTotal;

    public string CurrentUserName => _auth.Session.Customer?.DisplayName ?? "Гость";
    public bool IsAuthenticated => _auth.Session.IsSignedIn;
    public bool IsAuthVisible => _showAuth;
    public bool IsSignInPanel => IsAuthVisible && !IsRegisterMode;
    public bool IsRegisterPanel => IsAuthVisible && IsRegisterMode;
    public bool IsAppVisible => true;
    public bool IsCatalogVisible => CurrentSection == "Каталог";
    public bool IsCartVisible => CurrentSection == "Корзина";
    public bool IsOrdersVisible => CurrentSection == "Заказы" && IsAuthenticated;
    public bool IsPaymentVisible => CurrentSection == "Оплата";
    public bool IsGuest => !IsAuthenticated;
    public bool IsAccountButtonVisible => !IsAuthenticated;
    public bool IsSignOutButtonVisible => IsAuthenticated;
    public bool IsCatalogSelected => CurrentSection == "Каталог";
    public bool IsCartSelected => CurrentSection == "Корзина";
    public bool IsOrdersSelected => CurrentSection == "Заказы";
    public string AuthTitle => IsRegisterMode ? "Создать аккаунт" : "Войти для покупки";
    public string AuthSwitchText => IsRegisterMode ? "Уже есть аккаунт? Войти" : "Впервые здесь? Создать аккаунт";
    public string HasAccountText => IsAuthenticated ? "Локальный аккаунт" : "Покупка как гость";
    public string ThemeButtonText => IsDarkTheme ? "Светлая тема" : "Тёмная тема";
    public string ThemeButtonIcon => IsDarkTheme ? "/Assets/Icons/sun.svg" : "/Assets/Icons/moon.svg";
    public ThemeVariant CurrentThemeVariant => IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
    public bool HasNotice => !string.IsNullOrWhiteSpace(Notice);
    public bool HasProducts => FilteredProducts.Count > 0;
    public bool HasSelectedProduct => SelectedProduct is not null;
    public bool HasNoProducts => !HasProducts;
    public bool HasNoSelectedProduct => !HasSelectedProduct;
    public bool HasNoOrders => PreOrders.Count == 0;
    public bool HasNoCartItems => CartItems.Count == 0;
    public bool CanCheckout => CartItems.Count > 0 && !IsBusy;
    public string CartTotalText => FormatMoney(CartTotal);
    public string PaymentTotalText => CartTotalText;
    public string LastSyncText => LastSyncUtc is null
        ? "Каталог загружается из DummyJSON; данные кэшируются в SQLite"
        : $"Локальный каталог обновлён {LastSyncUtc.Value.ToLocalTime():dd.MM.yyyy HH:mm}";
    public string SelectedPriceText => SelectedProduct is null ? string.Empty : FormatMoney(SelectedProduct.Price);
    public string SelectedArrivalText => SelectedProduct is null ? string.Empty : ArrivalLabel(SelectedProduct.ExpectedArrivalDate);
    public string SelectedAvailabilityText => SelectedProduct is null
        ? string.Empty
        : SelectedProduct.IsAvailable ? $"Есть в наличии: {SelectedProduct.AvailableQuantity} шт." : "Нет в наличии";

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
        OnPropertyChanged(nameof(IsSignInPanel));
        OnPropertyChanged(nameof(IsRegisterPanel));
        OnPropertyChanged(nameof(AuthTitle));
        OnPropertyChanged(nameof(AuthSwitchText));
    }
    partial void OnIsDarkThemeChanged(bool value)
    {
        OnPropertyChanged(nameof(ThemeButtonText));
        OnPropertyChanged(nameof(ThemeButtonIcon));
        OnPropertyChanged(nameof(CurrentThemeVariant));
    }
    partial void OnNoticeChanged(string value) => OnPropertyChanged(nameof(HasNotice));
    partial void OnCartCountChanged(int value) => OnPropertyChanged(nameof(CanCheckout));
    partial void OnCartTotalChanged(decimal value)
    {
        OnPropertyChanged(nameof(CartTotalText));
        OnPropertyChanged(nameof(PaymentTotalText));
    }
    partial void OnLastSyncUtcChanged(DateTime? value) => OnPropertyChanged(nameof(LastSyncText));
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
        // Show local cache first; API refresh continues without blocking the desktop UI.
        _ = RefreshCatalogAsync();
    }

    [RelayCommand]
    private void Navigate(string? section)
    {
        if (section is "Каталог" or "Корзина" || section == "Заказы" && IsAuthenticated)
            CurrentSection = section;
    }

    [RelayCommand]
    private void SelectProduct(Product? product) => SelectedProduct = product;

    [RelayCommand]
    private void OpenAccount()
    {
        _pendingAuthCheckout = false;
        IsRegisterMode = false;
        _authReturnSection = CurrentSection;
        _showAuth = true;
        NotifyAuthPanels();
        Notice = "Войдите или создайте профиль. Каталог доступен и без аккаунта.";
    }

    [RelayCommand]
    private void SkipRegistration()
    {
        var destination = _pendingAuthCheckout ? "Корзина" : _authReturnSection;
        _showAuth = false;
        _pendingAuthCheckout = false;
        IsRegisterMode = false;
        NotifyAuthPanels();
        CurrentSection = destination;
        Notice = "Продолжаете как гость. Аккаунт понадобится только для оформления покупки.";
    }

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
        await AuthenticateAsync(() => _auth.RegisterAsync(DisplayName, Email, Password));
    }

    [RelayCommand]
    private async Task SignInAsync() =>
        await AuthenticateAsync(() => _auth.SignInAsync(Email, Password));

    private async Task AuthenticateAsync(Func<Task<RegistrationResult>> authenticate)
    {
        IsBusy = true;
        HasError = false;
        try
        {
            var result = await authenticate();
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
        CartItems.Clear();
        CartCount = 0;
        CartTotal = 0;
        Email = Password = PasswordConfirmation = string.Empty;
        CurrentSection = "Каталог";
        Notice = "Вы вышли. Каталог остаётся доступен как гостю.";
        NotifyAuthPanels();
    }

    [RelayCommand]
    private async Task RefreshCatalogAsync()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;
        IsBusy = true;
        HasError = false;
        Notice = "Обновляем товары из DummyJSON…";
        try
        {
            var count = await _sync.SynchronizeAsync();
            await LoadCatalogAsync();
            LastSyncUtc = DateTime.UtcNow;
            Notice = $"Получено и сохранено в SQLite товаров: {count}.";
        }
        catch (Exception ex)
        {
            await LoadCatalogAsync();
            HasError = Products.Count == 0;
            Notice = Products.Count == 0
                ? $"Не удалось загрузить каталог: {ex.Message}"
                : $"Сервер недоступен; показывается локальный кэш ({Products.Count} товаров).";
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
        if (product is null) return;
        if (!product.IsAvailable || product.AvailableQuantity < 1)
        {
            HasError = true;
            Notice = "Этот товар сейчас отмечен как отсутствующий.";
            return;
        }
        try
        {
            if (IsAuthenticated)
                await _store.AddToCartAsync(_auth.Session.Customer!.Id, product.Id);
            else
            {
                var existing = CartItems.FirstOrDefault(x => x.ProductId == product.Id);
                if (existing is null)
                    CartItems.Add(new CartItem { ProductId = product.Id, Product = product, Quantity = 1 });
                else
                {
                    var reserved = Products.FirstOrDefault(x => x.Id == product.Id)?.AvailableQuantity ?? product.AvailableQuantity;
                    if (existing.Quantity >= reserved) throw new PreOrderException($"Доступно: {reserved} шт.");
                    var index = CartItems.IndexOf(existing);
                    CartItems[index] = new CartItem { ProductId = product.Id, Product = product, Quantity = existing.Quantity + 1 };
                }
            }
            Notice = $"«{product.Name}» добавлен в корзину.";
            HasError = false;
            await LoadCartAsync();
        }
        catch (PreOrderException ex)
        {
            HasError = true;
            Notice = ex.Message;
        }
    }

    [RelayCommand]
    private async Task RemoveFromCartAsync(CartItem? item)
    {
        if (item is null) return;
        if (IsAuthenticated)
            await _store.RemoveFromCartAsync(_auth.Session.Customer!.Id, item.Id);
        else if (item.Quantity > 1)
        {
            var index = CartItems.IndexOf(item);
            CartItems[index] = new CartItem { ProductId = item.ProductId, Product = item.Product, Quantity = item.Quantity - 1 };
        }
        else
            CartItems.Remove(item);
        await LoadCartAsync();
    }

    [RelayCommand]
    private void Checkout()
    {
        if (CartItems.Count == 0) return;
        if (!IsAuthenticated)
        {
            _pendingAuthCheckout = true;
            _authReturnSection = "Корзина";
            _showAuth = true;
            IsRegisterMode = true;
            NotifyAuthPanels();
            Notice = "Войдите или создайте аккаунт, чтобы сохранить покупку и перейти к демо-оплате.";
            return;
        }
        CurrentSection = "Оплата";
        Notice = "Проверочный экран. Нажмите «Подтвердить демо-оплату» — реальные деньги не списываются.";
    }

    [RelayCommand]
    private async Task PayDemoAsync()
    {
        if (!IsAuthenticated)
        {
            CurrentSection = "Корзина";
            Checkout();
            return;
        }
        IsBusy = true;
        HasError = false;
        try
        {
            var orders = await _store.CheckoutAsync(_auth.Session.Customer!.Id, isDemoPaid: true);
            Notice = orders.Count == 1
                ? $"Демо-оплата успешна. Заказ №{orders[0].Id} сохранён; деньги не списывались."
                : $"Демо-оплата успешна. Сохранено заказов: {orders.Count}; деньги не списывались.";
            await LoadCartAsync();
            await LoadOrdersAsync();
            CurrentSection = "Заказы";
        }
        catch (Exception ex)
        {
            HasError = true;
            Notice = $"Покупку не удалось оформить: {ex.Message}";
            CurrentSection = "Корзина";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void CancelPayment()
    {
        CurrentSection = "Корзина";
        Notice = "Демо-оплата отменена. Корзина сохранена.";
    }

    [RelayCommand]
    private async Task CancelOrderAsync(PreOrder? order)
    {
        if (order is null || !IsAuthenticated) return;
        try
        {
            await _store.CancelPreOrderAsync(order.Id, _auth.Session.Customer!.Id);
            Notice = $"Предзаказ №{order.Id} отменён.";
            await LoadOrdersAsync();
        }
        catch (Exception ex)
        {
            HasError = true;
            Notice = ex.Message;
        }
    }

    [RelayCommand]
    private void ToggleTheme() => IsDarkTheme = !IsDarkTheme;

    private async Task EnterStoreAsync()
    {
        var guestItems = CartItems.Where(x => x.CustomerId == 0).Select(x => (x.ProductId, x.Quantity)).ToArray();
        _showAuth = false;
        Password = PasswordConfirmation = string.Empty;
        NotifyAuthPanels();
        await LoadCartAsync();
        foreach (var (productId, quantity) in guestItems)
        {
            for (var i = 0; i < quantity; i++)
            {
                try { await _store.AddToCartAsync(_auth.Session.Customer!.Id, productId); }
                catch (PreOrderException) { break; }
            }
        }
        await LoadCartAsync();
        OnPropertyChanged(nameof(CurrentUserName));
        OnPropertyChanged(nameof(HasAccountText));
        if (_pendingAuthCheckout)
        {
            _pendingAuthCheckout = false;
            CurrentSection = "Оплата";
            Notice = "Корзина сохранена в аккаунте. Подтвердите учебную оплату для завершения покупки.";
        }
        else
            CurrentSection = "Каталог";
    }

    private async Task LoadCartAsync()
    {
        if (IsAuthenticated)
        {
            var items = await _store.GetCartItemsAsync(_auth.Session.Customer!.Id);
            CartItems.Clear();
            foreach (var item in items) CartItems.Add(item);
        }
        CartCount = CartItems.Sum(x => x.Quantity);
        CartTotal = CartItems.Sum(x => x.Quantity * x.Product.Price);
        OnPropertyChanged(nameof(HasNoCartItems));
    }

    private async Task LoadOrdersAsync()
    {
        if (!IsAuthenticated) return;
        try
        {
            var orders = await _store.GetPreOrdersAsync(_auth.Session.Customer!.Id);
            PreOrders.Clear();
            foreach (var order in orders) PreOrders.Add(order);
            OnPropertyChanged(nameof(HasNoOrders));
        }
        catch (Exception ex)
        {
            HasError = true;
            Notice = $"Не удалось прочитать историю покупок: {ex.Message}";
        }
    }

    private async Task LoadCatalogAsync()
    {
        var items = await _store.GetProductsAsync();
        Products.Clear();
        foreach (var item in items) Products.Add(item);
        if (items.Count > 0)
            LastSyncUtc = items.Max(x => x.UpdatedAtUtc);
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
        if (SelectedAvailability == "Предзаказ доступен") query = query.Where(x => x.IsAvailable);
        else if (SelectedAvailability == "Нет в наличии") query = query.Where(x => !x.IsAvailable);
        query = SelectedSort switch
        {
            "По названию" => query.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase),
            "Сначала дешевле" => query.OrderBy(x => x.Price).ThenBy(x => x.Name),
            "Сначала дороже" => query.OrderByDescending(x => x.Price).ThenBy(x => x.Name),
            _ => query.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
        };
        FilteredProducts.Clear();
        foreach (var product in query) FilteredProducts.Add(product);
        OnPropertyChanged(nameof(HasProducts));
        OnPropertyChanged(nameof(HasNoProducts));
    }

    private void NotifyAuthPanels()
    {
        OnPropertyChanged(nameof(IsAuthVisible));
        OnPropertyChanged(nameof(IsSignInPanel));
        OnPropertyChanged(nameof(IsRegisterPanel));
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(IsGuest));
        OnPropertyChanged(nameof(IsAccountButtonVisible));
        OnPropertyChanged(nameof(IsSignOutButtonVisible));
        OnPropertyChanged(nameof(CurrentUserName));
        OnPropertyChanged(nameof(HasAccountText));
        NotifySections();
    }

    private void NotifySections()
    {
        OnPropertyChanged(nameof(IsCatalogVisible));
        OnPropertyChanged(nameof(IsCartVisible));
        OnPropertyChanged(nameof(IsOrdersVisible));
        OnPropertyChanged(nameof(IsPaymentVisible));
        OnPropertyChanged(nameof(IsCatalogSelected));
        OnPropertyChanged(nameof(IsCartSelected));
        OnPropertyChanged(nameof(IsOrdersSelected));
    }

    public static string FormatMoney(decimal value) => value.ToString("N2", RuCulture) + " ₽";
    public static string ArrivalLabel(DateTime? date) => date is null ? "Дата поступления уточняется" : $"Поставка ожидается {date.Value:dd.MM.yyyy}";
}
