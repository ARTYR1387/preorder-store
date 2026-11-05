using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PreOrderStore.Data;
using PreOrderStore.Domain;
using PreOrderStore.Infrastructure;
using PreOrderStore.Services;

namespace PreOrderStore.Tests;

public sealed class StoreServiceTests
{
    [Fact]
    public async Task PlacePreOrder_FixesPriceAndPersistsOrder()
    {
        await using var fixture = await Fixture.CreateAsync();
        var product = await fixture.AddProduct(price: 19.95m, stock: 5);

        var order = await fixture.Store.PlacePreOrderAsync(product.Id, 2);
        product.Price = 24.50m;
        await fixture.Db.SaveChangesAsync();

        var saved = await fixture.Db.PreOrders.AsNoTracking().SingleAsync();
        Assert.Equal(19.95m, saved.UnitPriceAtOrder);
        Assert.Equal(2, saved.Quantity);
        Assert.Equal(39.90m, saved.Total);
        Assert.Equal(PreOrderStatus.AwaitingConfirmation, order.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task PlacePreOrder_RejectsNonPositiveQuantity(int quantity)
    {
        await using var fixture = await Fixture.CreateAsync();
        var product = await fixture.AddProduct(price: 10m, stock: 5);

        await Assert.ThrowsAsync<PreOrderException>(() => fixture.Store.PlacePreOrderAsync(product.Id, quantity));
        Assert.Empty(await fixture.Db.PreOrders.ToListAsync());
    }

    [Fact]
    public async Task PlacePreOrder_RejectsQuantityAboveAvailableStock()
    {
        await using var fixture = await Fixture.CreateAsync();
        var product = await fixture.AddProduct(price: 10m, stock: 2);

        await Assert.ThrowsAsync<PreOrderException>(() => fixture.Store.PlacePreOrderAsync(product.Id, 3));
        Assert.Empty(await fixture.Db.PreOrders.ToListAsync());
    }

    [Fact]
    public async Task CancelPreOrder_ReleasesReservedQuantity()
    {
        await using var fixture = await Fixture.CreateAsync();
        var product = await fixture.AddProduct(price: 10m, stock: 1);
        var order = await fixture.Store.PlacePreOrderAsync(product.Id, 1);
        await Assert.ThrowsAsync<PreOrderException>(() => fixture.Store.PlacePreOrderAsync(product.Id, 1));

        await fixture.Store.CancelPreOrderAsync(order.Id);
        var second = await fixture.Store.PlacePreOrderAsync(product.Id, 1);

        Assert.Equal(2, second.Id);
        Assert.Equal(PreOrderStatus.Cancelled, await fixture.Db.PreOrders
            .Where(x => x.Id == order.Id).Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task CancelPreOrder_RejectsAlreadyCancelledOrder()
    {
        await using var fixture = await Fixture.CreateAsync();
        var product = await fixture.AddProduct(price: 10m, stock: 1);
        var order = await fixture.Store.PlacePreOrderAsync(product.Id, 1);
        await fixture.Store.CancelPreOrderAsync(order.Id);

        await Assert.ThrowsAsync<PreOrderException>(() => fixture.Store.CancelPreOrderAsync(order.Id));
    }

    [Fact]
    public async Task Checkout_CreatesCustomerOrderAtCurrentPriceAndClearsCart()
    {
        await using var fixture = await Fixture.CreateAsync();
        var product = await fixture.AddProduct(price: 19.95m, stock: 5);
        var customer = await fixture.AddCustomer("buyer@example.com");

        await fixture.Store.AddToCartAsync(customer.Id, product.Id);
        await fixture.Store.AddToCartAsync(customer.Id, product.Id);
        product.Price = 24.50m;
        await fixture.Db.SaveChangesAsync();

        var orders = await fixture.Store.CheckoutAsync(customer.Id);

        var order = Assert.Single(orders);
        Assert.Equal(customer.Id, order.CustomerId);
        Assert.Equal(2, order.Quantity);
        Assert.Equal(24.50m, order.UnitPriceAtOrder);
        Assert.Equal(49m, order.Total);
        Assert.Empty(await fixture.Store.GetCartItemsAsync(customer.Id));
        Assert.Single(await fixture.Store.GetPreOrdersAsync(customer.Id));
    }

    [Fact]
    public async Task Customer_CannotReadOrCancelAnotherCustomersOrder()
    {
        await using var fixture = await Fixture.CreateAsync();
        var product = await fixture.AddProduct(price: 10m, stock: 2);
        var buyer = await fixture.AddCustomer("buyer@example.com");
        var other = await fixture.AddCustomer("other@example.com");
        await fixture.Store.AddToCartAsync(buyer.Id, product.Id);
        var order = Assert.Single(await fixture.Store.CheckoutAsync(buyer.Id));

        Assert.Empty(await fixture.Store.GetPreOrdersAsync(other.Id));
        await Assert.ThrowsAsync<PreOrderException>(() => fixture.Store.CancelPreOrderAsync(order.Id, other.Id));
        await fixture.Store.CancelPreOrderAsync(order.Id, buyer.Id);
    }

    [Fact]
    public async Task Synchronization_UpsertsByExternalIdAndNeverDeletesHistoryProduct()
    {
        await using var fixture = await Fixture.CreateAsync();
        var product = await fixture.AddProduct(price: 10m, stock: 3);
        var order = await fixture.Store.PlacePreOrderAsync(product.Id, 1);
        var sync = new CatalogSyncService(fixture.Db, new StaticProductProvider(
        [
            new ProductFeedItem("42", "Updated item", "New description", "books", 12.25m, null, null, 3, true),
            new ProductFeedItem("43", "New item", "Description", "books", 5m, null, null, 2, true)
        ]));

        await sync.SynchronizeAsync();
        await sync.SynchronizeAsync();

        Assert.Equal(2, await fixture.Db.Products.CountAsync());
        var updated = await fixture.Db.Products.SingleAsync(x => x.ExternalId == "42");
        Assert.Equal("Updated item", updated.Name);
        Assert.Equal(order.Id, await fixture.Db.PreOrders.Select(x => x.Id).SingleAsync());
        Assert.Equal(10m, await fixture.Db.PreOrders.Select(x => x.UnitPriceAtOrder).SingleAsync());
    }

    private sealed class StaticProductProvider(IReadOnlyList<ProductFeedItem> items) : IProductProvider
    {
        public Task<IReadOnlyList<ProductFeedItem>> GetProductsAsync(CancellationToken cancellationToken = default) => Task.FromResult(items);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public StoreDbContext Db { get; }
        public StoreService Store { get; }

        private Fixture(SqliteConnection connection, StoreDbContext db)
        {
            _connection = connection;
            Db = db;
            Store = new StoreService(db);
        }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<StoreDbContext>().UseSqlite(connection).Options;
            var db = new StoreDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new Fixture(connection, db);
        }

        public async Task<Product> AddProduct(decimal price, int stock)
        {
            var now = DateTime.UtcNow;
            var product = new Product
            {
                ExternalId = "42", Name = "Test product", Description = "Description", Category = "books",
                Price = price, ExpectedArrivalDate = now.AddDays(14), AvailableQuantity = stock,
                IsAvailable = stock > 0, IsDemoData = true, CreatedAtUtc = now, UpdatedAtUtc = now
            };
            Db.Products.Add(product);
            await Db.SaveChangesAsync();
            return product;
        }

        public async Task<StoreCustomer> AddCustomer(string email)
        {
            var customer = new StoreCustomer
            {
                DisplayName = "Test Buyer", EmailNormalized = email.ToUpperInvariant(),
                PasswordSalt = "salt", PasswordHash = "hash", CreatedAtUtc = DateTime.UtcNow
            };
            Db.Customers.Add(customer);
            await Db.SaveChangesAsync();
            return customer;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
