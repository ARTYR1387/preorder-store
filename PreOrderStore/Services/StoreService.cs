using Microsoft.EntityFrameworkCore;
using PreOrderStore.Data;
using PreOrderStore.Domain;

namespace PreOrderStore.Services;

public sealed class StoreService(StoreDbContext db)
{
    public Task<List<Product>> GetProductsAsync(CancellationToken cancellationToken = default) =>
        db.Products.AsNoTracking().OrderBy(x => x.ExpectedArrivalDate == null)
            .ThenBy(x => x.ExpectedArrivalDate).ThenBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<List<PreOrder>> GetPreOrdersAsync(int? customerId = null, CancellationToken cancellationToken = default)
    {
        var query = db.PreOrders.AsNoTracking().Include(x => x.Product).AsQueryable();
        if (customerId is not null) query = query.Where(x => x.CustomerId == customerId);
        return query.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    public Task<List<CartItem>> GetCartItemsAsync(int customerId, CancellationToken cancellationToken = default) =>
        db.CartItems.AsNoTracking().Include(x => x.Product).Where(x => x.CustomerId == customerId)
            .OrderBy(x => x.Product.Name).ToListAsync(cancellationToken);

    public async Task AddToCartAsync(int customerId, int productId, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new PreOrderException("Новинка не найдена. Обновите каталог.");
        if (!product.IsAvailable || product.AvailableQuantity <= 0)
            throw new PreOrderException("Предзаказ на эту новинку пока не открыт.");

        var reserved = await db.PreOrders.Where(x => x.ProductId == productId && x.Status != PreOrderStatus.Cancelled)
            .SumAsync(x => (int?)x.Quantity, cancellationToken) ?? 0;
        var cartItem = await db.CartItems.SingleOrDefaultAsync(x => x.CustomerId == customerId && x.ProductId == productId, cancellationToken);
        var nextQuantity = (cartItem?.Quantity ?? 0) + 1;
        if (nextQuantity > product.AvailableQuantity - reserved)
            throw new PreOrderException($"Доступно для предзаказа: {Math.Max(0, product.AvailableQuantity - reserved)} шт.");

        if (cartItem is null)
            db.CartItems.Add(new CartItem { CustomerId = customerId, ProductId = productId, Quantity = 1 });
        else
            cartItem.Quantity++;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveFromCartAsync(int customerId, int cartItemId, CancellationToken cancellationToken = default)
    {
        var cartItem = await db.CartItems.SingleOrDefaultAsync(x => x.Id == cartItemId && x.CustomerId == customerId, cancellationToken)
            ?? throw new PreOrderException("Позиция уже отсутствует в корзине.");
        if (cartItem.Quantity > 1) cartItem.Quantity--;
        else db.CartItems.Remove(cartItem);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PreOrder>> CheckoutAsync(int customerId, bool isDemoPaid = false, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var cart = await db.CartItems.Include(x => x.Product).Where(x => x.CustomerId == customerId)
            .OrderBy(x => x.Id).ToListAsync(cancellationToken);
        if (cart.Count == 0) throw new PreOrderException("Корзина пуста.");

        foreach (var item in cart)
        {
            var product = item.Product;
            var reserved = await db.PreOrders.Where(x => x.ProductId == product.Id && x.Status != PreOrderStatus.Cancelled)
                .SumAsync(x => (int?)x.Quantity, cancellationToken) ?? 0;
            if (!product.IsAvailable || item.Quantity > product.AvailableQuantity - reserved)
                throw new PreOrderException($"«{product.Name}»: изменилось доступное количество. Проверьте корзину.");
        }

        var orders = cart.Select(item => new PreOrder
        {
            CustomerId = customerId,
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            UnitPriceAtOrder = item.Product.Price,
            CreatedAtUtc = DateTime.UtcNow,
            Status = PreOrderStatus.AwaitingConfirmation,
            IsDemoPaid = isDemoPaid
        }).ToArray();
        db.PreOrders.AddRange(orders);
        db.CartItems.RemoveRange(cart);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return orders;
    }

    // Kept for service tests and migration compatibility; application checkout always supplies a customer.
    public Task<PreOrder> PlacePreOrderAsync(int productId, int quantity, CancellationToken cancellationToken = default) =>
        CreatePreOrderAsync(null, productId, quantity, cancellationToken);

    private async Task<PreOrder> CreatePreOrderAsync(int? customerId, int productId, int quantity, CancellationToken cancellationToken)
    {
        if (quantity <= 0) throw new PreOrderException("Количество должно быть больше нуля.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new PreOrderException("Товар не найден. Обновите каталог.");
        if (!product.IsAvailable || product.AvailableQuantity <= 0)
            throw new PreOrderException("Предзаказ на этот товар сейчас недоступен.");
        var alreadyReserved = await db.PreOrders.Where(x => x.ProductId == productId && x.Status != PreOrderStatus.Cancelled)
            .SumAsync(x => (int?)x.Quantity, cancellationToken) ?? 0;
        if (quantity > product.AvailableQuantity - alreadyReserved)
            throw new PreOrderException($"Доступно для предзаказа: {Math.Max(0, product.AvailableQuantity - alreadyReserved)} шт.");

        var order = new PreOrder
        {
            CustomerId = customerId,
            ProductId = product.Id,
            Quantity = quantity,
            UnitPriceAtOrder = product.Price,
            CreatedAtUtc = DateTime.UtcNow,
            Status = PreOrderStatus.AwaitingConfirmation
        };
        db.PreOrders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return order;
    }

    public async Task CancelPreOrderAsync(int preOrderId, int? customerId = null, CancellationToken cancellationToken = default)
    {
        var query = db.PreOrders.AsQueryable();
        if (customerId is not null) query = query.Where(x => x.CustomerId == customerId);
        var order = await query.SingleOrDefaultAsync(x => x.Id == preOrderId, cancellationToken)
            ?? throw new PreOrderException("Предзаказ не найден.");
        if (order.Status != PreOrderStatus.AwaitingConfirmation)
            throw new PreOrderException("Отменить можно только предзаказ, ожидающий подтверждения.");
        order.Status = PreOrderStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);
    }
}
