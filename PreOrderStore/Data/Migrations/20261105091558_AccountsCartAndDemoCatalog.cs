using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreOrderStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccountsCartAndDemoCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "Products",
                newName: "ImageAssetPath");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpectedArrivalDate",
                table: "Products",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AddColumn<bool>(
                name: "IsDemoData",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "PreOrders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EmailNormalized = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    PasswordSalt = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CartItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartItems", x => x.Id);
                    table.CheckConstraint("CK_CartItems_Quantity_Positive", "Quantity > 0");
                    table.ForeignKey(
                        name: "FK_CartItems_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CartItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PreOrders_CustomerId",
                table: "PreOrders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CustomerId_ProductId",
                table: "CartItems",
                columns: new[] { "CustomerId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_ProductId",
                table: "CartItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_EmailNormalized",
                table: "Customers",
                column: "EmailNormalized",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PreOrders_Customers_CustomerId",
                table: "PreOrders",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PreOrders_Customers_CustomerId",
                table: "PreOrders");

            migrationBuilder.DropTable(
                name: "CartItems");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_PreOrders_CustomerId",
                table: "PreOrders");

            migrationBuilder.DropColumn(
                name: "IsDemoData",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "PreOrders");

            migrationBuilder.RenameColumn(
                name: "ImageAssetPath",
                table: "Products",
                newName: "ImageUrl");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpectedArrivalDate",
                table: "Products",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
