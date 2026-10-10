using HomeApp.AI.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HomeApp.AI.Migrations;

[DbContext(typeof(HomeAppAiDbContext))]
[Migration("20261010150000_SetLunaDatabaseDefault")]
public sealed class SetLunaDatabaseDefault : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        UPDATE "llm" SET "IsDefault" = ("Key" = 'gpt-6-luna') WHERE EXISTS (SELECT 1 FROM "llm" WHERE "Key" = 'gpt-6-luna' AND "IsEnabled");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
