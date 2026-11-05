using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace PreOrderStore.Domain;

public enum PreOrderStatus
{
    AwaitingConfirmation = 0,
    Confirmed = 1,
    Cancelled = 2,
    Fulfilled = 3
}

public sealed class StoreCustomer
{
    public int Id { get; set; }
    public required string DisplayName { get; set; }
    public required string EmailNormalized { get; set; }
    public required string PasswordSalt { get; set; }
    public required string PasswordHash { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ICollection<PreOrder> PreOrders { get; set; } = new List<PreOrder>();
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<FavoriteItem> Favorites { get; set; } = new List<FavoriteItem>();
}

public sealed class Product : INotifyPropertyChanged
{
    private bool _isFavorite;
    private int _cartQuantity;
    public event PropertyChangedEventHandler? PropertyChanged;

    public int Id { get; set; }
    public required string ExternalId { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public required string Category { get; set; }
    public decimal Price { get; set; }
    public string? ImageAssetPath { get; set; }
    public DateTime? ExpectedArrivalDate { get; set; }
    public int AvailableQuantity { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsDemoData { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public ICollection<PreOrder> PreOrders { get; set; } = new List<PreOrder>();
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<FavoriteItem> Favorites { get; set; } = new List<FavoriteItem>();

    [NotMapped]
    public string PriceDisplay => Price.ToString("N0", CultureInfo.GetCultureInfo("ru-RU")) + " ₽";

    [NotMapped]
    public string ArrivalDisplay => ExpectedArrivalDate is null ? "Дата выхода уточняется" : $"Ожидается {ExpectedArrivalDate:dd.MM.yyyy}";

    [NotMapped]
    public string ReleaseStateDisplay => IsAvailable ? "В наличии" : "Нет в наличии";

    [NotMapped]
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite == value) return;
            _isFavorite = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFavorite)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FavoriteIconPath)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FavoriteButtonLabel)));
        }
    }

    [NotMapped]
    public int CartQuantity
    {
        get => _cartQuantity;
        set
        {
            if (_cartQuantity == value) return;
            _cartQuantity = Math.Max(0, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CartQuantity)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInCart)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CartActionIconPath)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CartActionTooltip)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CartActionEnabled)));
        }
    }

    [NotMapped]
    public bool IsInCart => CartQuantity > 0;

    [NotMapped]
    public string CartActionIconPath => IsInCart ? "/Assets/Icons/minus.svg" : "/Assets/Icons/plus.svg";

    [NotMapped]
    public string CartActionTooltip => IsInCart ? "Убрать один товар из корзины" : "Добавить в корзину";

    [NotMapped]
    public bool CartActionEnabled => IsInCart || IsAvailable;

    [NotMapped]
    public string FavoriteIconPath => IsFavorite ? "/Assets/Icons/heart-filled.svg" : "/Assets/Icons/heart.svg";

    [NotMapped]
    public string FavoriteButtonLabel => IsFavorite ? "Убрать из избранного" : "В избранное";

    [NotMapped]
    public bool IsLocalProduct => ExternalId.StartsWith("local-", StringComparison.Ordinal);

    [NotMapped]
    public string CategoryDisplay => Category.ToLowerInvariant() switch
    {
        "beauty" => "Красота",
        "fragrances" => "Парфюмерия",
        "furniture" => "Мебель",
        "groceries" => "Продукты",
        "home-decoration" => "Декор для дома",
        "kitchen-accessories" => "Аксессуары для кухни",
        "laptops" => "Ноутбуки",
        "mens-shirts" => "Мужские футболки",
        "mens-shoes" => "Мужская обувь",
        "mens-watches" => "Мужские часы",
        "mobile-accessories" => "Аксессуары для смартфонов",
        "motorcycle" => "Мотоциклы",
        "skin-care" => "Уход за кожей",
        "smartphones" => "Смартфоны",
        "sports-accessories" => "Спортивные товары",
        "sunglasses" => "Солнцезащитные очки",
        "tablets" => "Планшеты",
        "tops" => "Одежда",
        "vehicle" => "Автомобили",
        "womens-bags" => "Женские сумки",
        "womens-dresses" => "Женские платья",
        "womens-jewellery" => "Украшения",
        "womens-shoes" => "Женская обувь",
        "womens-watches" => "Женские часы",
        _ => Category
    };
}

public sealed class CartItem : INotifyPropertyChanged
{
    private bool _isEntering;
    private bool _isRemoving;
    public event PropertyChangedEventHandler? PropertyChanged;

    public int Id { get; set; }
    public int CustomerId { get; set; }
    public StoreCustomer Customer { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }

    [NotMapped]
    public bool IsEntering
    {
        get => _isEntering;
        set { if (_isEntering == value) return; _isEntering = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEntering))); }
    }

    [NotMapped]
    public bool IsRemoving
    {
        get => _isRemoving;
        set { if (_isRemoving == value) return; _isRemoving = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRemoving))); }
    }

    [NotMapped]
    public string LineTotalDisplay => (Quantity * Product.Price).ToString("N0", CultureInfo.GetCultureInfo("ru-RU")) + " ₽";
}

public sealed class FavoriteItem
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public StoreCustomer Customer { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
}

public sealed class PreOrder
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public StoreCustomer? Customer { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPriceAtOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public PreOrderStatus Status { get; set; }
    public bool IsDemoPaid { get; set; }

    [NotMapped]
    public string StatusDisplayName => Status switch
    {
        PreOrderStatus.AwaitingConfirmation => "Ожидает подтверждения",
        PreOrderStatus.Confirmed => "Подтверждён",
        PreOrderStatus.Cancelled => "Отменён",
        PreOrderStatus.Fulfilled => "Выполнен",
        _ => "Неизвестен"
    };

    [NotMapped]
    public bool CanCancel => Status == PreOrderStatus.AwaitingConfirmation;

    [NotMapped]
    public decimal Total => UnitPriceAtOrder * Quantity;

    [NotMapped]
    public string TotalDisplay => Total.ToString("N0", CultureInfo.GetCultureInfo("ru-RU")) + " ₽";

    [NotMapped]
    public string PaymentDisplay => IsDemoPaid ? "Оплачено через Demo Bank · демо" : "Оплата не подтверждена";
}

public sealed record RegistrationResult(bool Succeeded, string Message);

public sealed class PreOrderException(string message) : Exception(message);
