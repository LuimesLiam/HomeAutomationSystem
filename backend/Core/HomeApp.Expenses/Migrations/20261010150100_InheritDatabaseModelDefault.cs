using HomeApp.Expenses.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HomeApp.Expenses.Migrations;

[DbContext(typeof(HomeAppExpensesDbContext))]
[Migration("20261010150100_InheritDatabaseModelDefault")]
public sealed class InheritDatabaseModelDefault : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        UPDATE "ExpenseAgentSettings" SET "ModelKey" = NULL, "UpdatedAtUtc" = CURRENT_TIMESTAMP WHERE "ModelKey" = 'gpt-6-luna';
        """);

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
