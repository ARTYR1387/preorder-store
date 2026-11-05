using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreOrderStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class DemoPaymentFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDemoPaid",
                table: "PreOrders",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDemoPaid",
                table: "PreOrders");
        }
    }
}
