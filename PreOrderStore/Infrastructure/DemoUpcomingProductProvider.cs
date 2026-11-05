namespace PreOrderStore.Infrastructure;

public interface IProductProvider
{
    Task<IReadOnlyList<ProductFeedItem>> GetProductsAsync(CancellationToken cancellationToken = default);
}

public sealed record ProductFeedItem(
    string ExternalId,
    string Name,
    string Description,
    string Category,
    decimal Price,
    string? ImageAssetPath,
    DateTime? ExpectedArrivalDate,
    int AvailableQuantity,
    bool IsAvailable);

/// <summary>
/// Offline demo feed. Names and prices are sample data, not manufacturer announcements or a live stock promise.
/// Replace with a verified merchant/manufacturer feed for a real launch catalogue.
/// </summary>
public sealed class DemoUpcomingProductProvider : IProductProvider
{
    private static readonly ProductFeedItem[] Items =
    [
        new("demo-iphone-duo", "iPhone Duo", "Демонстрационная позиция по примеру пользователя. Модель, характеристики, цена и сроки не подтверждены производителем.", "Смартфоны", 199_990m, "/Assets/Products/iphone-duo.svg", null, 24, true),
        new("demo-fold-phone", "Складной смартфон · демо", "Пример карточки ожидаемой новинки. Дата и характеристики уточняются; цена приведена только для демонстрации оформления.", "Смартфоны", 149_990m, "/Assets/Products/fold-phone.svg", null, 12, true),
        new("demo-console", "Игровая консоль нового поколения · демо", "Демонстрационная модель магазина. Это не анонс производителя; заказ в приложении не является реальной покупкой.", "Игровые устройства", 79_990m, "/Assets/Products/console.svg", null, 0, false),
        new("demo-audio", "Наушники нового поколения · демо", "Пример предстоящего релиза с условной ценой. Дата выхода и спецификация не заявляются.", "Аудио", 29_990m, "/Assets/Products/headphones.svg", null, 30, true)
    ];

    public Task<IReadOnlyList<ProductFeedItem>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ProductFeedItem>>(Items);
    }
}
