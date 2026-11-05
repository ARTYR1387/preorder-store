using Microsoft.EntityFrameworkCore;
using PreOrderStore.Domain;

namespace PreOrderStore.Data;

public sealed class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<PreOrder> PreOrders => Set<PreOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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

        var preOrder = modelBuilder.Entity<PreOrder>();
        preOrder.HasKey(x => x.Id);
        preOrder.Property(x => x.UnitPriceAtOrder).HasPrecision(12, 2);
        preOrder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        preOrder.ToTable(table => table.HasCheckConstraint("CK_PreOrders_Quantity_Positive", "Quantity > 0"));
    }
}
