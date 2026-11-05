using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreOrderStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: true),
                    ExpectedArrivalDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AvailableQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    IsAvailable = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PreOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitPriceAtOrder = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreOrders", x => x.Id);
                    table.CheckConstraint("CK_PreOrders_Quantity_Positive", "Quantity > 0");
                    table.ForeignKey(
                        name: "FK_PreOrders_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PreOrders_ProductId",
                table: "PreOrders",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ExternalId",
                table: "Products",
                column: "ExternalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PreOrders");

            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
