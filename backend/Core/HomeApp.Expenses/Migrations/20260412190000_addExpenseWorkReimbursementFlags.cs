using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeApp.Expenses.Migrations
{
    /// <inheritdoc />
    public partial class addExpenseWorkReimbursementFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReimbursable",
                table: "ExpenseEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsWorkExpense",
                table: "ExpenseEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReimbursable",
                table: "ExpenseEntries");

            migrationBuilder.DropColumn(
                name: "IsWorkExpense",
                table: "ExpenseEntries");
        }
    }
}
