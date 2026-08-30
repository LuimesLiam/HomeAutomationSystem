using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeApp.Videos.Migrations
{
    /// <inheritdoc />
    public partial class watch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Episodes_SeriesId",
                table: "Episodes");

            migrationBuilder.AddColumn<bool>(
                name: "Watched",
                table: "Movies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Watched",
                table: "Episodes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Series_Genre",
                table: "Series",
                column: "Genre");

            migrationBuilder.CreateIndex(
                name: "IX_Series_Hidden_Title",
                table: "Series",
                columns: new[] { "Hidden", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_Series_Name",
                table: "Series",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Movies_FilePath",
                table: "Movies",
                column: "FilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Movies_Genre",
                table: "Movies",
                column: "Genre");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_Hidden_Title",
                table: "Movies",
                columns: new[] { "Hidden", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_Movies_Year",
                table: "Movies",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_FilePath",
                table: "Episodes",
                column: "FilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_Hidden",
                table: "Episodes",
                column: "Hidden");

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_SeriesId_Season_EpisodeNumber",
                table: "Episodes",
                columns: new[] { "SeriesId", "Season", "EpisodeNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Series_Genre",
                table: "Series");

            migrationBuilder.DropIndex(
                name: "IX_Series_Hidden_Title",
                table: "Series");

            migrationBuilder.DropIndex(
                name: "IX_Series_Name",
                table: "Series");

            migrationBuilder.DropIndex(
                name: "IX_Movies_FilePath",
                table: "Movies");

            migrationBuilder.DropIndex(
                name: "IX_Movies_Genre",
                table: "Movies");

            migrationBuilder.DropIndex(
                name: "IX_Movies_Hidden_Title",
                table: "Movies");

            migrationBuilder.DropIndex(
                name: "IX_Movies_Year",
                table: "Movies");

            migrationBuilder.DropIndex(
                name: "IX_Episodes_FilePath",
                table: "Episodes");

            migrationBuilder.DropIndex(
                name: "IX_Episodes_Hidden",
                table: "Episodes");

            migrationBuilder.DropIndex(
                name: "IX_Episodes_SeriesId_Season_EpisodeNumber",
                table: "Episodes");

            migrationBuilder.DropColumn(
                name: "Watched",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "Watched",
                table: "Episodes");

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_SeriesId",
                table: "Episodes",
                column: "SeriesId");
        }
    }
}
