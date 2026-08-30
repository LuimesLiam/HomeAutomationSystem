using HomeApp.Videos.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HomeApp.Videos.Migrations
{
    [DbContext(typeof(HomeAppVideosDbContext))]
    [Migration("20260404000000_mediaSources")]
    public partial class mediaSources : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    Path = table.Column<string>(type: "text", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaSources", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaSources_SourceType_DisplayOrder",
                table: "MediaSources",
                columns: new[] { "SourceType", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaSources_SourceType_Path",
                table: "MediaSources",
                columns: new[] { "SourceType", "Path" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaSources");
        }
    }
}
