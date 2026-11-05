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
    private string _productReturnSection = "Каталог";
    private const int ProductPageSize = 12;
    private List<Product> _matchingProducts = [];
    private readonly HashSet<int> _guestFavoriteProductIds = [];
    private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

    public List<Product> Products { get; } = [];
    public ObservableCollection<Product> FilteredProducts { get; } = [];
    public ObservableCollection<CartItem> CartItems { get; } = [];
    public ObservableCollection<PreOrder> PreOrders { get; } = [];
    public ObservableCollection<string> Categories { get; } = ["Все категории"];
    public IReadOnlyList<string> AvailabilityOptions { get; } = ["Любой статус", "В наличии", "Нет в наличии"];
    public IReadOnlyList<string> SortOptions { get; } = ["По популярному каталогу", "По названию", "Сначала дешевле", "Сначала дороже"];
    public IReadOnlyList<string> ProductIllustrationOptions { get; } = ["Аудио", "Смартфон", "Складной смартфон", "Игровая консоль"];

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
    [ObservableProperty] private bool _cartCountPulse;
    [ObservableProperty] private bool _isPageEntering;
    [ObservableProperty] private string _newProductName = string.Empty;
    [ObservableProperty] private string _newProductCategory = string.Empty;
    [ObservableProperty] private string _newProductPrice = string.Empty;
    [ObservableProperty] private string _newProductStock = "5";
    [ObservableProperty] private string _newProductDescription = string.Empty;
    [ObservableProperty] private string _newProductImageUrl = string.Empty;
    [ObservableProperty] private string _selectedProductIllustration = "Аудио";

    public string CurrentUserName => _auth.Session.Customer?.DisplayName ?? "Гость";
    public bool IsAuthenticated => _auth.Session.IsSignedIn;
    public bool IsAuthVisible => _showAuth;
    public bool IsSignInPanel => IsAuthVisible && !IsRegisterMode;
    public bool IsRegisterPanel => IsAuthVisible && IsRegisterMode;
    public bool IsAppVisible => true;
    public bool IsCatalogVisible => CurrentSection is "Каталог" or "Избранное";
    public bool IsCartVisible => CurrentSection == "Корзина";
    public bool IsOrdersVisible => CurrentSection == "Заказы" && IsAuthenticated;
    public bool IsPaymentVisible => CurrentSection == "Оплата";
    public bool IsProductDetailsVisible => CurrentSection == "Товар";
    public bool IsManageProductVisible => CurrentSection == "Добавить товар";
    public bool IsGuest => !IsAuthenticated;
    public bool IsAccountButtonVisible => !IsAuthenticated;
    public bool IsSignOutButtonVisible => IsAuthenticated;
    public bool IsCatalogSelected => CurrentSection == "Каталог";
    public bool IsCartSelected => CurrentSection == "Корзина";
    public bool IsOrdersSelected => CurrentSection == "Заказы";
    public bool IsFavoritesSelected => CurrentSection == "Избранное";
    public bool IsLocalProductSelected => SelectedProduct?.IsLocalProduct == true;
    public string CatalogSectionTitle => CurrentSection == "Избранное" ? "Избранное" : "Каталог товаров";
    public string SelectedProductIllustrationPath => SelectedProductIllustration switch
    {
        "Смартфон" => "/Assets/Products/iphone-duo.svg",
        "Складной смартфон" => "/Assets/Products/fold-phone.svg",
        "Игровая консоль" => "/Assets/Products/console.svg",
        _ => "/Assets/Products/headphones.svg"
    };
    public string AuthTitle => IsRegisterMode ? "Создать аккаунт" : "Войти для покупки";
    public string AuthSwitchText => IsRegisterMode ? "Уже есть аккаунт? Войти" : "Впервые здесь? Создать аккаунт";
    public string HasAccountText => IsAuthenticated ? "Локальный аккаунт" : "Покупка как гость";
    public string ThemeButtonText => IsDarkTheme ? "Светлая тема" : "Тёмная тема";
    public string ThemeButtonIcon => IsDarkTheme ? "/Assets/Icons/sun.svg" : "/Assets/Icons/moon.svg";
    public ThemeVariant CurrentThemeVariant => IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
    public bool HasNotice => !string.IsNullOrWhiteSpace(Notice);
    public bool HasProducts => _matchingProducts.Count > 0;
    public bool HasSelectedProduct => SelectedProduct is not null;
    public bool HasNoProducts => !HasProducts;
    public bool CanLoadMoreProducts => FilteredProducts.Count < _matchingProducts.Count;
    public string FilteredProductsCountText => FormatProductCount(_matchingProducts.Count);
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
    public string SelectedCartActionText => SelectedProduct is null ? "Добавить в корзину" :
        CartItems.FirstOrDefault(x => x.ProductId == SelectedProduct.Id) is { Quantity: > 0 } item
            ? $"В корзине · ещё {item.Quantity} шт." : "Добавить в корзину";

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
    partial void OnSelectedProductIllustrationChanged(string value) => OnPropertyChanged(nameof(SelectedProductIllustrationPath));
    partial void OnCurrentSectionChanged(string value)
    {
        NotifySections();
        ApplyFilters();
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
        OnPropertyChanged(nameof(IsLocalProductSelected));
        OnPropertyChanged(nameof(SelectedCartActionText));
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
        if (section is "Каталог" or "Избранное" or "Корзина" or "Добавить товар" || section == "Заказы" && IsAuthenticated)
            CurrentSection = section;
    }

    [RelayCommand]
    private void SelectProduct(Product? product) => SelectedProduct = product;

    [RelayCommand]
    private async Task OpenProductAsync(Product? product)
    {
        if (product is null) return;
        if (CurrentSection is "Каталог" or "Избранное") _productReturnSection = CurrentSection;
        SelectedProduct = product;
        CurrentSection = "Товар";
        await AnimatePageEntryAsync();
    }

    [RelayCommand]
    private async Task BackFromProductAsync()
    {
        CurrentSection = _productReturnSection;
        await AnimatePageEntryAsync();
    }

    [RelayCommand]
    private async Task OpenAddProductAsync()
    {
        CurrentSection = "Добавить товар";
        Notice = "Добавьте собственную учебную позицию. Каталог DummyJSON останется без изменений.";
        await AnimatePageEntryAsync();
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(Product? product)
    {
        if (product is null) return;
        var next = !product.IsFavorite;
        product.IsFavorite = next;
        try
        {
            if (IsAuthenticated)
                await _store.SetFavoriteAsync(_auth.Session.Customer!.Id, product.Id, next);
            else if (next)
                _guestFavoriteProductIds.Add(product.Id);
            else
                _guestFavoriteProductIds.Remove(product.Id);

            Notice = next ? $"«{product.Name}» добавлен в избранное." : $"«{product.Name}» убран из избранного.";
            ApplyFilters();
        }
        catch (Exception ex)
        {
            product.IsFavorite = !next;
            HasError = true;
            Notice = $"Не удалось обновить избранное: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CreateLocalProductAsync()
    {
        if (!decimal.TryParse(NewProductPrice, NumberStyles.Number, RuCulture, out var price) &&
            !decimal.TryParse(NewProductPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out price))
        {
            HasError = true;
            Notice = "Введите корректную цену, например 12990 или 12990,50.";
            return;
        }
        if (!int.TryParse(NewProductStock, NumberStyles.Integer, RuCulture, out var stock))
        {
            HasError = true;
            Notice = "Остаток должен быть целым числом.";
            return;
        }

        IsBusy = true;
        HasError = false;
        try
        {
            var product = await _store.CreateLocalProductAsync(
                NewProductName, NewProductCategory, price, stock, NewProductDescription,
                string.IsNullOrWhiteSpace(NewProductImageUrl) ? SelectedProductIllustrationPath : NewProductImageUrl.Trim());
            NewProductName = NewProductCategory = NewProductPrice = NewProductDescription = NewProductImageUrl = string.Empty;
            NewProductStock = "5";
            await LoadCatalogAsync();
            SelectedProduct = Products.FirstOrDefault(x => x.Id == product.Id);
            _productReturnSection = "Каталог";
            CurrentSection = "Товар";
            Notice = $"«{product.Name}» добавлен в локальный каталог.";
            await AnimatePageEntryAsync();
        }
        catch (Exception ex)
        {
            HasError = true;
            Notice = $"Не удалось добавить товар: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteLocalProductAsync()
    {
        if (SelectedProduct is not { IsLocalProduct: true } product) return;
        IsBusy = true;
        try
        {
            await _store.DeleteLocalProductAsync(product.Id);
            _guestFavoriteProductIds.Remove(product.Id);
            SelectedProduct = null;
            await LoadCatalogAsync();
            CurrentSection = "Каталог";
            Notice = $"«{product.Name}» удалён из локального каталога.";
        }
        catch (Exception ex)
        {
            HasError = true;
            Notice = ex.Message;
        }
        finally { IsBusy = false; }
    }

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
        foreach (var product in Products) product.IsFavorite = _guestFavoriteProductIds.Contains(product.Id);
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
                    CartItems.Add(new CartItem { ProductId = product.Id, Product = product, Quantity = 1, IsEntering = true });
                else
                {
                    var reserved = Products.FirstOrDefault(x => x.Id == product.Id)?.AvailableQuantity ?? product.AvailableQuantity;
                    if (existing.Quantity >= reserved) throw new PreOrderException($"Доступно: {reserved} шт.");
                    var index = CartItems.IndexOf(existing);
                    CartItems[index] = new CartItem { ProductId = product.Id, Product = product, Quantity = existing.Quantity + 1, IsEntering = true };
                }
            }
            Notice = $"«{product.Name}» добавлен в корзину.";
            HasError = false;
            await LoadCartAsync(animateProductId: product.Id);
            await PulseCartCountAsync();
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
        if (item is null || item.IsRemoving) return;
        item.IsRemoving = true;
        await Task.Delay(200);
        try
        {
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
        catch (Exception ex)
        {
            item.IsRemoving = false;
            HasError = true;
            Notice = $"Не удалось изменить корзину: {ex.Message}";
        }
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
        var guestFavorites = Products.Where(x => x.IsFavorite).Select(x => x.Id).ToArray();
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
        foreach (var productId in guestFavorites)
        {
            try { await _store.SetFavoriteAsync(_auth.Session.Customer!.Id, productId, true); }
            catch (PreOrderException) { }
        }
        _guestFavoriteProductIds.Clear();
        await LoadCartAsync();
        await LoadCatalogAsync();
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

    private async Task LoadCartAsync(int? animateProductId = null)
    {
        if (IsAuthenticated)
        {
            var items = await _store.GetCartItemsAsync(_auth.Session.Customer!.Id);
            CartItems.Clear();
            foreach (var item in items)
            {
                if (item.ProductId == animateProductId) item.IsEntering = true;
                CartItems.Add(item);
            }
        }
        CartCount = CartItems.Sum(x => x.Quantity);
        CartTotal = CartItems.Sum(x => x.Quantity * x.Product.Price);
        OnPropertyChanged(nameof(HasNoCartItems));
        OnPropertyChanged(nameof(SelectedCartActionText));
        if (animateProductId is not null)
        {
            await Task.Delay(20);
            var enteringItem = CartItems.FirstOrDefault(x => x.ProductId == animateProductId);
            if (enteringItem is not null)
            {
                await Task.Delay(260);
                enteringItem.IsEntering = false;
            }
        }
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
        var favoriteIds = IsAuthenticated
            ? await _store.GetFavoriteProductIdsAsync(_auth.Session.Customer!.Id)
            : _guestFavoriteProductIds;
        Products.Clear();
        foreach (var item in items)
        {
            item.IsFavorite = favoriteIds.Contains(item.Id);
            Products.Add(item);
        }
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
        if (CurrentSection == "Избранное") query = query.Where(x => x.IsFavorite);
        if (!string.IsNullOrWhiteSpace(SearchText))
            query = query.Where(x => x.Name.Contains(SearchText.Trim(), StringComparison.CurrentCultureIgnoreCase));
        if (SelectedCategory != "Все категории") query = query.Where(x => x.Category == SelectedCategory);
        if (SelectedAvailability == "В наличии") query = query.Where(x => x.IsAvailable);
        else if (SelectedAvailability == "Нет в наличии") query = query.Where(x => !x.IsAvailable);
        query = SelectedSort switch
        {
            "По названию" => query.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase),
            "Сначала дешевле" => query.OrderBy(x => x.Price).ThenBy(x => x.Name),
            "Сначала дороже" => query.OrderByDescending(x => x.Price).ThenBy(x => x.Name),
            _ => query.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
        };
        _matchingProducts = query.ToList();
        FilteredProducts.Clear();
        foreach (var product in _matchingProducts.Take(ProductPageSize)) FilteredProducts.Add(product);
        OnPropertyChanged(nameof(CanLoadMoreProducts));
        OnPropertyChanged(nameof(FilteredProductsCountText));
        OnPropertyChanged(nameof(HasProducts));
        OnPropertyChanged(nameof(HasNoProducts));
    }

    [RelayCommand]
    private void LoadMoreProducts()
    {
        if (!CanLoadMoreProducts) return;
        foreach (var product in _matchingProducts.Skip(FilteredProducts.Count).Take(ProductPageSize))
            FilteredProducts.Add(product);
        OnPropertyChanged(nameof(CanLoadMoreProducts));
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
        OnPropertyChanged(nameof(IsProductDetailsVisible));
        OnPropertyChanged(nameof(IsManageProductVisible));
        OnPropertyChanged(nameof(IsCatalogSelected));
        OnPropertyChanged(nameof(IsCartSelected));
        OnPropertyChanged(nameof(IsOrdersSelected));
        OnPropertyChanged(nameof(IsFavoritesSelected));
        OnPropertyChanged(nameof(CatalogSectionTitle));
    }

    private async Task AnimatePageEntryAsync()
    {
        IsPageEntering = true;
        await Task.Delay(16);
        IsPageEntering = false;
    }

    private async Task PulseCartCountAsync()
    {
        CartCountPulse = true;
        await Task.Delay(180);
        CartCountPulse = false;
    }

    public static string FormatMoney(decimal value) => value.ToString("N2", RuCulture) + " ₽";
    private static string FormatProductCount(int count)
    {
        var lastTwo = count % 100;
        var last = count % 10;
        var word = last == 1 && lastTwo != 11 ? "позиция" : last is >= 2 and <= 4 && lastTwo is not (>= 12 and <= 14) ? "позиции" : "позиций";
        return $"{count} {word}";
    }
    public static string ArrivalLabel(DateTime? date) => date is null ? "Дата поступления уточняется" : $"Поставка ожидается {date.Value:dd.MM.yyyy}";
}
