using Microsoft.EntityFrameworkCore;
using PreOrderStore.Domain;

namespace PreOrderStore.Data;

public sealed class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
{
    public DbSet<StoreCustomer> Customers => Set<StoreCustomer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<FavoriteItem> Favorites => Set<FavoriteItem>();
    public DbSet<PreOrder> PreOrders => Set<PreOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var customer = modelBuilder.Entity<StoreCustomer>();
        customer.HasKey(x => x.Id);
        customer.HasIndex(x => x.EmailNormalized).IsUnique();
        customer.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        customer.Property(x => x.EmailNormalized).HasMaxLength(254).IsRequired();
        customer.Property(x => x.PasswordSalt).HasMaxLength(128).IsRequired();
        customer.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();

        var product = modelBuilder.Entity<Product>();
        product.HasKey(x => x.Id);
        product.HasIndex(x => x.ExternalId).IsUnique();
        product.Property(x => x.ExternalId).HasMaxLength(80).IsRequired();
        product.Property(x => x.Name).HasMaxLength(240).IsRequired();
        product.Property(x => x.Category).HasMaxLength(120).IsRequired();
        product.Property(x => x.Price).HasPrecision(12, 2);
        product.Property(x => x.Description).HasMaxLength(4000);
        product.HasMany(x => x.PreOrders)
            .WithOne(x => x.Product)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        var order = modelBuilder.Entity<PreOrder>();
        order.HasKey(x => x.Id);
        order.Property(x => x.UnitPriceAtOrder).HasPrecision(12, 2);
        order.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        order.HasOne(x => x.Customer).WithMany(x => x.PreOrders)
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        order.ToTable(table => table.HasCheckConstraint("CK_PreOrders_Quantity_Positive", "Quantity > 0"));

        var cart = modelBuilder.Entity<CartItem>();
        cart.HasKey(x => x.Id);
        cart.HasIndex(x => new { x.CustomerId, x.ProductId }).IsUnique();
        cart.HasOne(x => x.Customer).WithMany(x => x.CartItems)
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        cart.HasOne(x => x.Product).WithMany(x => x.CartItems)
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        cart.ToTable(table => table.HasCheckConstraint("CK_CartItems_Quantity_Positive", "Quantity > 0"));

        var favorite = modelBuilder.Entity<FavoriteItem>();
        favorite.HasKey(x => x.Id);
        favorite.HasIndex(x => new { x.CustomerId, x.ProductId }).IsUnique();
        favorite.HasOne(x => x.Customer).WithMany(x => x.Favorites)
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        favorite.HasOne(x => x.Product).WithMany(x => x.Favorites)
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}
