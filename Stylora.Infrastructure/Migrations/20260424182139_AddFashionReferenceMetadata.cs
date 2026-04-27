using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stylora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFashionReferenceMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArticleType",
                table: "ClothingReferenceEmbeddings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseColour",
                table: "ClothingReferenceEmbeddings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoryGroup",
                table: "ClothingReferenceEmbeddings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ColorFamily",
                table: "ClothingReferenceEmbeddings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "ClothingReferenceEmbeddings",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GenderTag",
                table: "ClothingReferenceEmbeddings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MasterCategory",
                table: "ClothingReferenceEmbeddings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeasonTag",
                table: "ClothingReferenceEmbeddings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceDataset",
                table: "ClothingReferenceEmbeddings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubCategory",
                table: "ClothingReferenceEmbeddings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsageTag",
                table: "ClothingReferenceEmbeddings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClothingReferenceEmbeddings_ArticleType",
                table: "ClothingReferenceEmbeddings",
                column: "ArticleType");

            migrationBuilder.CreateIndex(
                name: "IX_ClothingReferenceEmbeddings_CategoryGroup",
                table: "ClothingReferenceEmbeddings",
                column: "CategoryGroup");

            migrationBuilder.CreateIndex(
                name: "IX_ClothingReferenceEmbeddings_ColorFamily",
                table: "ClothingReferenceEmbeddings",
                column: "ColorFamily");

            migrationBuilder.CreateIndex(
                name: "IX_ClothingReferenceEmbeddings_GenderTag",
                table: "ClothingReferenceEmbeddings",
                column: "GenderTag");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClothingReferenceEmbeddings_ArticleType",
                table: "ClothingReferenceEmbeddings");

            migrationBuilder.DropIndex(
                name: "IX_ClothingReferenceEmbeddings_CategoryGroup",
                table: "ClothingReferenceEmbeddings");

            migrationBuilder.DropIndex(
                name: "IX_ClothingReferenceEmbeddings_ColorFamily",
                table: "ClothingReferenceEmbeddings");

            migrationBuilder.DropIndex(
                name: "IX_ClothingReferenceEmbeddings_GenderTag",
                table: "ClothingReferenceEmbeddings");

            migrationBuilder.DropColumn(
                name: "ArticleType",
                table: "ClothingReferenceEmbeddings");

            migrationBuilder.DropColumn(
                name: "BaseColour",
                table: "ClothingReferenceEmbeddings");

            migrationBuilder.DropColumn(
                name: "CategoryGroup",
                table: "ClothingReferenceEmbeddings");

            migrationBuilder.DropColumn(
                name: "ColorFamily",
                table: "ClothingReferenceEmbeddings");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "ClothingReferenceEmbeddings");

            migrationBuilder.DropColumn(
                name: "GenderTag",
                table: "ClothingReferenceEmbeddings");

            migrationBuilder.DropColumn(
                name: "MasterCategory",
                table: "ClothingReferenceEmbeddings");

            migrationBuilder.DropColumn(
                name: "SeasonTag",
                table: "ClothingReferenceEmbeddings");

            migrationBuilder.DropColumn(
                name: "SourceDataset",
                table: "ClothingReferenceEmbeddings");

            migrationBuilder.DropColumn(
                name: "SubCategory",
                table: "ClothingReferenceEmbeddings");

            migrationBuilder.DropColumn(
                name: "UsageTag",
                table: "ClothingReferenceEmbeddings");
        }
    }
}
