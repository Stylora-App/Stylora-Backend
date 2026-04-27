using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stylora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWardrobeItemMetadataLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArticleTypeLabel",
                table: "WardrobeItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AudienceTag",
                table: "WardrobeItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WardrobeItems_ArticleTypeLabel",
                table: "WardrobeItems",
                column: "ArticleTypeLabel");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WardrobeItems_ArticleTypeLabel",
                table: "WardrobeItems");

            migrationBuilder.DropColumn(
                name: "ArticleTypeLabel",
                table: "WardrobeItems");

            migrationBuilder.DropColumn(
                name: "AudienceTag",
                table: "WardrobeItems");
        }
    }
}
