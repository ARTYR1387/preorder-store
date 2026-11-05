using Microsoft.EntityFrameworkCore;
using PreOrderStore.Data;
using PreOrderStore.Domain;

namespace PreOrderStore.Services;

public sealed class StoreService(StoreDbContext db)
{
    public Task<List<Product>> GetProductsAsync(CancellationToken cancellationToken = default) =>
        db.Products.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<List<PreOrder>> GetPreOrdersAsync(CancellationToken cancellationToken = default) =>
        db.PreOrders.AsNoTracking().Include(x => x.Product)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public async Task<PreOrder> PlacePreOrderAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
            throw new PreOrderException("Количество должно быть больше нуля.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new PreOrderException("Товар не найден. Обновите каталог.");

        if (!product.IsAvailable || product.AvailableQuantity <= 0)
            throw new PreOrderException("Предзаказ на этот товар сейчас недоступен.");

        var alreadyReserved = await db.PreOrders
            .Where(x => x.ProductId == productId && x.Status != PreOrderStatus.Cancelled)
            .SumAsync(x => (int?)x.Quantity, cancellationToken) ?? 0;

        if (quantity > product.AvailableQuantity - alreadyReserved)
            throw new PreOrderException($"Доступно для предзаказа: {Math.Max(0, product.AvailableQuantity - alreadyReserved)} шт.");

        var preOrder = new PreOrder
        {
            ProductId = product.Id,
            Quantity = quantity,
            UnitPriceAtOrder = product.Price,
            CreatedAtUtc = DateTime.UtcNow,
            Status = PreOrderStatus.AwaitingConfirmation
        };
        db.PreOrders.Add(preOrder);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return preOrder;
    }

    public async Task CancelPreOrderAsync(int preOrderId, CancellationToken cancellationToken = default)
    {
        var order = await db.PreOrders.SingleOrDefaultAsync(x => x.Id == preOrderId, cancellationToken)
            ?? throw new PreOrderException("Предзаказ не найден.");
        if (order.Status != PreOrderStatus.AwaitingConfirmation)
            throw new PreOrderException("Отменить можно только предзаказ, ожидающий подтверждения.");

        order.Status = PreOrderStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);
    }
}
