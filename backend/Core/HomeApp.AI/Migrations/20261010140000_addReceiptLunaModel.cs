using HomeApp.AI.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HomeApp.AI.Migrations;

[DbContext(typeof(HomeAppAiDbContext))]
[Migration("20261010140000_addReceiptLunaModel")]
public sealed class AddReceiptLunaModel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        INSERT INTO "llm" ("Key", "Name", "ModelName", "Provider", "BaseUrl", "ApiKeyName", "ParamsJson", "IsEnabled", "IsDefault")
        VALUES ('gpt-6-luna', 'GPT-6 Luna', 'gpt-6-luna', 'OpenAI', 'https://api.openai.com/v1/', 'OPENAI_API_KEY', '{"maxOutputTokens":16384}', true, false)
        ON CONFLICT ("Key") DO NOTHING;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
