using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Stylora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClothingImageValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidatedAt",
                table: "WardrobeItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ValidationConfidence",
                table: "WardrobeItems",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationMessage",
                table: "WardrobeItems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationStatus",
                table: "WardrobeItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClothingReferenceEmbeddings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    CategoryHint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Embedding = table.Column<Vector>(type: "vector(512)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClothingReferenceEmbeddings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClothingReferenceEmbeddings_Embedding",
                table: "ClothingReferenceEmbeddings",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_ClothingReferenceEmbeddings_Label",
                table: "ClothingReferenceEmbeddings",
                column: "Label");

            migrationBuilder.CreateIndex(
                name: "IX_ClothingReferenceEmbeddings_SourceKey",
                table: "ClothingReferenceEmbeddings",
                column: "SourceKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClothingReferenceEmbeddings");

            migrationBuilder.DropColumn(
                name: "ValidatedAt",
                table: "WardrobeItems");

            migrationBuilder.DropColumn(
                name: "ValidationConfidence",
                table: "WardrobeItems");

            migrationBuilder.DropColumn(
                name: "ValidationMessage",
                table: "WardrobeItems");

            migrationBuilder.DropColumn(
                name: "ValidationStatus",
                table: "WardrobeItems");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
