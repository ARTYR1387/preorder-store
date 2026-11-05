using Microsoft.EntityFrameworkCore;
using PreOrderStore.Data;
using PreOrderStore.Domain;
using PreOrderStore.Infrastructure;

namespace PreOrderStore.Services;

public sealed class CatalogSyncService(StoreDbContext db, IProductProvider provider)
{
    public async Task<int> SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        var feed = await provider.GetProductsAsync(cancellationToken);
        if (feed.Count == 0) throw new InvalidDataException("DummyJSON вернул пустой список товаров.");
        var externalIds = feed.Select(x => x.ExternalId).ToArray();
        var existing = await db.Products
            .Where(x => externalIds.Contains(x.ExternalId))
            .ToDictionaryAsync(x => x.ExternalId, cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var item in feed)
        {
            if (existing.TryGetValue(item.ExternalId, out var product))
            {
                product.Name = item.Name;
                product.Description = item.Description;
                product.Category = item.Category;
                product.Price = item.Price;
                product.ImageAssetPath = item.ImageAssetPath;
                product.ExpectedArrivalDate = item.ExpectedArrivalDate;
                product.AvailableQuantity = item.AvailableQuantity;
                product.IsAvailable = item.IsAvailable && item.AvailableQuantity > 0;
                product.IsDemoData = true;
                product.UpdatedAtUtc = now;
                continue;
            }

            db.Products.Add(new Product
            {
                ExternalId = item.ExternalId,
                Name = item.Name,
                Description = item.Description,
                Category = item.Category,
                Price = item.Price,
                ImageAssetPath = item.ImageAssetPath,
                ExpectedArrivalDate = item.ExpectedArrivalDate,
                AvailableQuantity = item.AvailableQuantity,
                IsAvailable = item.IsAvailable && item.AvailableQuantity > 0,
                IsDemoData = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        var obsoleteConcepts = await db.Products
            .Where(x => x.ExternalId.StartsWith("demo-") &&
                        !x.PreOrders.Any() && !x.CartItems.Any())
            .ToListAsync(cancellationToken);
        db.Products.RemoveRange(obsoleteConcepts);

        await db.SaveChangesAsync(cancellationToken);
        return feed.Count;
    }
}
