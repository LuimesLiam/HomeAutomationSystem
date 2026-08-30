using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeApp.Expenses.Migrations
{
    /// <inheritdoc />
    public partial class addExpenseCsvImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DuplicateFingerprint",
                table: "ExpenseEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportFileName",
                table: "ExpenseEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportSchemaKey",
                table: "ExpenseEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportSourceReference",
                table: "ExpenseEntries",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseEntries_DuplicateFingerprint",
                table: "ExpenseEntries",
                column: "DuplicateFingerprint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExpenseEntries_DuplicateFingerprint",
                table: "ExpenseEntries");

            migrationBuilder.DropColumn(
                name: "DuplicateFingerprint",
                table: "ExpenseEntries");

            migrationBuilder.DropColumn(
                name: "ImportFileName",
                table: "ExpenseEntries");

            migrationBuilder.DropColumn(
                name: "ImportSchemaKey",
                table: "ExpenseEntries");

            migrationBuilder.DropColumn(
                name: "ImportSourceReference",
                table: "ExpenseEntries");
        }
    }
}
