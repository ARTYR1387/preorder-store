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

    public async Task<HashSet<int>> GetFavoriteProductIdsAsync(int customerId, CancellationToken cancellationToken = default) =>
        (await db.Favorites.AsNoTracking().Where(x => x.CustomerId == customerId)
            .Select(x => x.ProductId).ToListAsync(cancellationToken)).ToHashSet();

    public async Task SetFavoriteAsync(int customerId, int productId, bool isFavorite, CancellationToken cancellationToken = default)
    {
        var favorite = await db.Favorites.SingleOrDefaultAsync(x => x.CustomerId == customerId && x.ProductId == productId, cancellationToken);
        if (isFavorite)
        {
            if (favorite is not null) return;
            if (!await db.Products.AnyAsync(x => x.Id == productId, cancellationToken))
                throw new PreOrderException("Товар больше не найден в каталоге.");
            db.Favorites.Add(new FavoriteItem { CustomerId = customerId, ProductId = productId });
        }
        else if (favorite is not null)
            db.Favorites.Remove(favorite);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Product> CreateLocalProductAsync(
        string name, string category, decimal price, int stock, string description, string? imageAssetPath,
        CancellationToken cancellationToken = default)
    {
        name = name.Trim();
        category = category.Trim();
        description = description.Trim();
        if (name.Length is < 2 or > 240) throw new PreOrderException("Название должно содержать от 2 до 240 символов.");
        if (category.Length is < 2 or > 120) throw new PreOrderException("Категория должна содержать от 2 до 120 символов.");
        if (price < 0 || price > 9_999_999_999.99m) throw new PreOrderException("Укажите корректную цену в рублях.");
        if (stock is < 0 or > 1_000_000) throw new PreOrderException("Остаток должен быть от 0 до 1 000 000.");
        if (description.Length > 4000) throw new PreOrderException("Описание не должно превышать 4000 символов.");

        if (string.IsNullOrWhiteSpace(imageAssetPath))
            imageAssetPath = "/Assets/Products/headphones.svg";
        var isLocalIllustration = imageAssetPath.StartsWith("/Assets/Products/", StringComparison.Ordinal);
        var isHttpsImage = Uri.TryCreate(imageAssetPath, UriKind.Absolute, out var imageUri) && imageUri.Scheme == Uri.UriSchemeHttps;
        if (!isLocalIllustration && !isHttpsImage)
            throw new PreOrderException("Для изображения используйте локальную SVG-иллюстрацию или HTTPS-ссылку.");

        var now = DateTime.UtcNow;
        var product = new Product
        {
            ExternalId = $"local-{Guid.NewGuid():N}",
            Name = name,
            Description = description,
            Category = category,
            Price = decimal.Round(price, 2, MidpointRounding.AwayFromZero),
            ImageAssetPath = imageAssetPath,
            AvailableQuantity = stock,
            IsAvailable = stock > 0,
            IsDemoData = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return product;
    }

    public async Task DeleteLocalProductAsync(int productId, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new PreOrderException("Товар уже удалён.");
        if (!product.IsLocalProduct)
            throw new PreOrderException("Товары API нельзя удалить из источника. Можно удалять только добавленные вручную товары.");
        if (await db.PreOrders.AnyAsync(x => x.ProductId == productId, cancellationToken) ||
            await db.CartItems.AnyAsync(x => x.ProductId == productId, cancellationToken))
            throw new PreOrderException("Нельзя удалить товар, который находится в корзине или истории покупок.");
        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);
    }

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
