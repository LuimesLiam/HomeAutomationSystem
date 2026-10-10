using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HomeApp.AI.Migrations
{
    /// <inheritdoc />
    public partial class dbControlledLlms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "llm",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ModelName = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    BaseUrl = table.Column<string>(type: "text", nullable: false),
                    ApiKeyName = table.Column<string>(type: "text", nullable: false),
                    ParamsJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_llm_Key",
                table: "llm",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_llm_Provider_ModelName",
                table: "llm",
                columns: new[] { "Provider", "ModelName" });

            migrationBuilder.Sql("""
                INSERT INTO "llm"
                    ("Id", "Key", "Name", "ModelName", "Provider", "BaseUrl", "ApiKeyName",
                     "ParamsJson", "IsEnabled", "IsDefault")
                SELECT
                    model."Id",
                    model."Key",
                    model."Name",
                    model."ModelId",
                    provider."Name",
                    provider."BaseUrl",
                    provider."ApiKeyEnvironmentVariableName",
                    jsonb_strip_nulls(jsonb_build_object(
                        'temperature', model."Temperature",
                        'maxOutputTokens', model."MaxOutputTokens",
                        'configuration', model."ConfigurationJson",
                        'providerConfiguration', provider."ConfigurationJson")),
                    model."IsEnabled" AND provider."IsEnabled",
                    model."IsDefault"
                FROM "AiModels" AS model
                INNER JOIN "AiProviders" AS provider ON provider."Id" = model."ProviderId";

                SELECT setval(
                    pg_get_serial_sequence('"llm"', 'Id'),
                    COALESCE((SELECT MAX("Id") FROM "llm"), 1),
                    EXISTS (SELECT 1 FROM "llm"));
                """);

            migrationBuilder.DropTable(name: "AiModels");
            migrationBuilder.DropTable(name: "AiProviders");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiProviders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApiKeyEnvironmentVariableName = table.Column<string>(type: "text", nullable: false),
                    BaseUrl = table.Column<string>(type: "text", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ProviderType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiModels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProviderId = table.Column<int>(type: "integer", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "text", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    MaxOutputTokens = table.Column<int>(type: "integer", nullable: true),
                    ModelId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Temperature = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiModels_AiProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "AiProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiModels_Key",
                table: "AiModels",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiModels_ProviderId_ModelId",
                table: "AiModels",
                columns: new[] { "ProviderId", "ModelId" });

            migrationBuilder.CreateIndex(
                name: "IX_AiProviders_Key",
                table: "AiProviders",
                column: "Key",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "AiProviders"
                    ("Id", "ApiKeyEnvironmentVariableName", "BaseUrl", "ConfigurationJson",
                     "IsEnabled", "Key", "Name", "ProviderType")
                SELECT
                    "Id", "ApiKeyName", "BaseUrl", NULL, "IsEnabled",
                    "Key" || '-provider', "Provider", "Provider"
                FROM "llm";

                INSERT INTO "AiModels"
                    ("Id", "ProviderId", "ConfigurationJson", "IsDefault", "IsEnabled",
                     "Key", "MaxOutputTokens", "ModelId", "Name", "Temperature")
                SELECT
                    "Id", "Id", "ParamsJson"::text, "IsDefault", "IsEnabled", "Key", NULL,
                    "ModelName", "Name", NULL
                FROM "llm";
                """);

            migrationBuilder.DropTable(name: "llm");
        }
    }
}
