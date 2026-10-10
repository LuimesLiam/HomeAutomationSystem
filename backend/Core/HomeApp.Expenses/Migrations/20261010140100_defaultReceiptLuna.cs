using HomeApp.Expenses.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HomeApp.Expenses.Migrations;

[DbContext(typeof(HomeAppExpensesDbContext))]
[Migration("20261010140100_defaultReceiptLuna")]
public sealed class DefaultReceiptLuna : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        UPDATE "ExpenseAgentSettings" SET "ModelKey" = 'gpt-6-luna', "UpdatedAtUtc" = CURRENT_TIMESTAMP;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
