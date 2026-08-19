using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishLeitner.EFDesign.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSlugColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Words",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Words_Slug",
                table: "Words",
                column: "Slug");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Words_Slug",
                table: "Words");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Words");
        }
    }
}
