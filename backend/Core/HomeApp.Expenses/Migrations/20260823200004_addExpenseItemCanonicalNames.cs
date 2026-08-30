using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeApp.Expenses.Migrations
{
    /// <inheritdoc />
    public partial class addExpenseItemCanonicalNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CanonicalName",
                table: "ExpenseLineItems",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE \"ExpenseLineItems\" SET \"CanonicalName\" = \"Name\" WHERE \"CanonicalName\" = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanonicalName",
                table: "ExpenseLineItems");
        }
    }
}
