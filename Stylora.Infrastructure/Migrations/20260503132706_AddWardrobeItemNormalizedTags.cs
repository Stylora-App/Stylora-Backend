using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stylora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWardrobeItemNormalizedTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ColorFamily",
                table: "WardrobeItems",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsageTag",
                table: "WardrobeItems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WardrobeItems_AudienceTag",
                table: "WardrobeItems",
                column: "AudienceTag");

            migrationBuilder.CreateIndex(
                name: "IX_WardrobeItems_ColorFamily",
                table: "WardrobeItems",
                column: "ColorFamily");

            migrationBuilder.CreateIndex(
                name: "IX_WardrobeItems_UsageTag",
                table: "WardrobeItems",
                column: "UsageTag");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WardrobeItems_AudienceTag",
                table: "WardrobeItems");

            migrationBuilder.DropIndex(
                name: "IX_WardrobeItems_ColorFamily",
                table: "WardrobeItems");

            migrationBuilder.DropIndex(
                name: "IX_WardrobeItems_UsageTag",
                table: "WardrobeItems");

            migrationBuilder.DropColumn(
                name: "ColorFamily",
                table: "WardrobeItems");

            migrationBuilder.DropColumn(
                name: "UsageTag",
                table: "WardrobeItems");
        }
    }
}
