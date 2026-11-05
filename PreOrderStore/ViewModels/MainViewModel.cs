using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PreOrderStore.Domain;
using PreOrderStore.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace PreOrderStore.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly StoreService _store;
    private readonly CatalogSyncService _sync;
    private bool _isRefreshing;

    public ObservableCollection<Product> Products { get; } = [];
    public ObservableCollection<Product> FilteredProducts { get; } = [];
    public ObservableCollection<PreOrder> PreOrders { get; } = [];
    public ObservableCollection<string> Categories { get; } = ["Все категории"];
    public IReadOnlyList<string> AvailabilityOptions { get; } = ["Любой статус", "Доступен", "Недоступен"];
    public IReadOnlyList<string> SortOptions { get; } = ["Сначала ближайшие", "По названию", "Сначала дешевле", "Сначала дороже"];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedCategory = "Все категории";
    [ObservableProperty] private string _selectedAvailability = "Любой статус";
    [ObservableProperty] private string _selectedSort = "Сначала ближайшие";
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private int _orderQuantity = 1;
    [ObservableProperty] private string _currentSection = "Каталог";
    [ObservableProperty] private string _notice = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private DateTime? _lastSyncUtc;

    public bool IsCatalogVisible => CurrentSection == "Каталог";
    public bool IsOrdersVisible => CurrentSection == "Предзаказы";
    public bool HasProducts => FilteredProducts.Count > 0;
    public bool HasSelectedProduct => SelectedProduct is not null;
    public bool HasNoProducts => !HasProducts;
    public bool HasNoSelectedProduct => !HasSelectedProduct;
    public bool HasNoOrders => PreOrders.Count == 0;
    public string LastSyncText => LastSyncUtc is null
        ? "Каталог ещё не обновлялся"
        : $"Обновлено {LastSyncUtc.Value.ToLocalTime():dd.MM.yyyy HH:mm}";
    public string SelectedPriceText => SelectedProduct is null ? string.Empty : FormatPrice(SelectedProduct.Price);
    public string SelectedArrivalText => SelectedProduct is null
        ? string.Empty
        : $"Ожидается {SelectedProduct.ExpectedArrivalDate:dd MMMM yyyy}";
    public string SelectedAvailabilityText => SelectedProduct is null
        ? string.Empty
        : SelectedProduct.IsAvailable ? $"Доступно: {SelectedProduct.AvailableQuantity} шт." : "Предзаказ недоступен";
    public bool CanPlaceOrder => SelectedProduct is { IsAvailable: true, AvailableQuantity: > 0 } && OrderQuantity > 0 && !IsBusy;

    public MainViewModel(StoreService store, CatalogSyncService sync)
    {
        _store = store;
        _sync = sync;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilters();
    partial void OnSelectedAvailabilityChanged(string value) => ApplyFilters();
    partial void OnSelectedSortChanged(string value) => ApplyFilters();
    partial void OnCurrentSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsCatalogVisible));
        OnPropertyChanged(nameof(IsOrdersVisible));
        if (value == "Предзаказы") _ = LoadOrdersAsync();
    }
    partial void OnSelectedProductChanged(Product? value)
    {
        OnPropertyChanged(nameof(SelectedPriceText));
        OnPropertyChanged(nameof(SelectedArrivalText));
        OnPropertyChanged(nameof(SelectedAvailabilityText));
        OnPropertyChanged(nameof(CanPlaceOrder));
        OnPropertyChanged(nameof(HasSelectedProduct));
        OnPropertyChanged(nameof(HasNoSelectedProduct));
    }
    partial void OnOrderQuantityChanged(int value) => OnPropertyChanged(nameof(CanPlaceOrder));
    partial void OnLastSyncUtcChanged(DateTime? value) => OnPropertyChanged(nameof(LastSyncText));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanPlaceOrder));

    public async Task InitializeAsync()
    {
        await LoadCatalogAsync();
        if (Products.Count == 0)
            await RefreshCatalogAsync();
    }

    [RelayCommand]
    private void Navigate(string? section)
    {
        if (section is "Каталог" or "Предзаказы") CurrentSection = section;
    }

    [RelayCommand]
    private void SelectProduct(Product? product)
    {
        SelectedProduct = product;
        OrderQuantity = 1;
    }

    [RelayCommand]
    private async Task RefreshCatalogAsync()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;
        IsBusy = true;
        HasError = false;
        Notice = "Обновляем каталог";
        try
        {
            var count = await _sync.SynchronizeAsync();
            await LoadCatalogAsync();
            LastSyncUtc = DateTime.UtcNow;
            Notice = $"Каталог обновлён. Получено товаров: {count}.";
        }
        catch (HttpRequestException)
        {
            HasError = true;
            Notice = Products.Count > 0
                ? "Нет соединения с каталогом. Показаны сохранённые товары."
                : "Не удалось загрузить каталог. Проверьте подключение к интернету.";
        }
        catch (TaskCanceledException)
        {
            HasError = true;
            Notice = "Время ожидания ответа каталога истекло. Попробуйте ещё раз.";
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
    private async Task PlaceOrderAsync()
    {
        if (SelectedProduct is null) return;
        IsBusy = true;
        try
        {
            var order = await _store.PlacePreOrderAsync(SelectedProduct.Id, OrderQuantity);
            Notice = $"Предзаказ №{order.Id} оформлен и ожидает подтверждения.";
            OrderQuantity = 1;
            await LoadOrdersAsync();
            await LoadCatalogAsync();
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
        if (order is null) return;
        try
        {
            await _store.CancelPreOrderAsync(order.Id);
            Notice = $"Предзаказ №{order.Id} отменён.";
            await LoadOrdersAsync();
            await LoadCatalogAsync();
        }
        catch (Exception ex)
        {
            HasError = true;
            Notice = ex.Message;
        }
    }

    [RelayCommand]
    private async Task LoadOrdersAsync()
    {
        try
        {
            var orders = await _store.GetPreOrdersAsync();
            PreOrders.Clear();
            foreach (var order in orders) PreOrders.Add(order);
            OnPropertyChanged(nameof(HasNoOrders));
        }
        catch (Exception ex)
        {
            HasError = true;
            Notice = $"Не удалось прочитать предзаказы: {ex.Message}";
        }
    }

    private async Task LoadCatalogAsync()
    {
        var items = await _store.GetProductsAsync();
        Products.Clear();
        foreach (var item in items) Products.Add(item);
        LastSyncUtc = Products.Count == 0 ? null : Products.Max(x => x.UpdatedAtUtc);
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
        var query = Products.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchText))
            query = query.Where(x => x.Name.Contains(SearchText.Trim(), StringComparison.CurrentCultureIgnoreCase));
        if (!string.IsNullOrWhiteSpace(SelectedCategory) && SelectedCategory != "Все категории")
            query = query.Where(x => x.Category == SelectedCategory);
        if (SelectedAvailability == "Доступен") query = query.Where(x => x.IsAvailable);
        else if (SelectedAvailability == "Недоступен") query = query.Where(x => !x.IsAvailable);

        query = SelectedSort switch
        {
            "По названию" => query.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase),
            "Сначала дешевле" => query.OrderBy(x => x.Price).ThenBy(x => x.Name),
            "Сначала дороже" => query.OrderByDescending(x => x.Price).ThenBy(x => x.Name),
            _ => query.OrderBy(x => x.ExpectedArrivalDate).ThenBy(x => x.Name)
        };
        FilteredProducts.Clear();
        foreach (var product in query) FilteredProducts.Add(product);
        OnPropertyChanged(nameof(HasProducts));
        OnPropertyChanged(nameof(HasNoProducts));
    }

    private static string FormatPrice(decimal price) =>
        price.ToString("N2", CultureInfo.GetCultureInfo("en-US")) + " USD";
}
