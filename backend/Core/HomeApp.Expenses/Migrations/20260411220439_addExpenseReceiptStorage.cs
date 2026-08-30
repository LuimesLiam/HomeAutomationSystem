using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeApp.Expenses.Migrations
{
    /// <inheritdoc />
    public partial class addExpenseReceiptStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ReceiptImageBytes",
                table: "ExpenseEntries",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptImageContentType",
                table: "ExpenseEntries",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiptImageBytes",
                table: "ExpenseEntries");

            migrationBuilder.DropColumn(
                name: "ReceiptImageContentType",
                table: "ExpenseEntries");
        }
    }
}
