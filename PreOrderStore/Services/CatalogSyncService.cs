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
                product.ImageUrl = item.ImageUrl;
                product.AvailableQuantity = item.AvailableQuantity;
                product.IsAvailable = item.AvailableQuantity > 0;
                product.UpdatedAtUtc = now;
                continue;
            }

            var numericId = int.TryParse(item.ExternalId, out var parsed) ? parsed : 0;
            db.Products.Add(new Product
            {
                ExternalId = item.ExternalId,
                Name = item.Name,
                Description = item.Description,
                Category = item.Category,
                Price = item.Price,
                ImageUrl = item.ImageUrl,
                AvailableQuantity = item.AvailableQuantity,
                IsAvailable = item.AvailableQuantity > 0,
                ExpectedArrivalDate = now.Date.AddDays(14 + Math.Abs(numericId % 28)),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return feed.Count;
    }
}
