using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreOrderStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProductManagementAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "Customers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE \"Customers\" SET \"IsAdmin\" = 1 WHERE \"Id\" = (SELECT \"Id\" FROM \"Customers\" ORDER BY \"CreatedAtUtc\", \"Id\" LIMIT 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "Customers");
        }
    }
}
