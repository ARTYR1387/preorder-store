using System.ComponentModel.DataAnnotations.Schema;

namespace PreOrderStore.Domain;

public enum PreOrderStatus
{
    AwaitingConfirmation = 0,
    Confirmed = 1,
    Cancelled = 2,
    Fulfilled = 3
}

public sealed class Product
{
    public int Id { get; set; }
    public required string ExternalId { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public required string Category { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime ExpectedArrivalDate { get; set; }
    public int AvailableQuantity { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public ICollection<PreOrder> PreOrders { get; set; } = new List<PreOrder>();
}

public sealed class PreOrder
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPriceAtOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public PreOrderStatus Status { get; set; }

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
}

public sealed class PreOrderException(string message) : Exception(message);
