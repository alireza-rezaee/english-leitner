using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishLeitner.EFDesign.Data.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Words",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HeadWord = table.Column<string>(type: "TEXT", nullable: true),
                    Position = table.Column<string>(type: "TEXT", nullable: true),
                    Grammar = table.Column<string>(type: "TEXT", nullable: true),
                    Cefr = table.Column<int>(type: "INTEGER", nullable: true),
                    WebPage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Words", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MeaningGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Head = table.Column<string>(type: "TEXT", nullable: true),
                    WordId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeaningGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeaningGroups_Words_WordId",
                        column: x => x.WordId,
                        principalTable: "Words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pronunciations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Culture = table.Column<int>(type: "INTEGER", nullable: false),
                    Phonetics = table.Column<string>(type: "TEXT", nullable: true),
                    Mp3Url = table.Column<string>(type: "TEXT", nullable: true),
                    OggUrl = table.Column<string>(type: "TEXT", nullable: true),
                    WordId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pronunciations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pronunciations_Words_WordId",
                        column: x => x.WordId,
                        principalTable: "Words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeaningItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<int>(type: "INTEGER", nullable: true),
                    Cefr = table.Column<int>(type: "INTEGER", nullable: true),
                    Grammar = table.Column<string>(type: "TEXT", nullable: true),
                    Definition = table.Column<string>(type: "TEXT", nullable: true),
                    Variants = table.Column<string>(type: "TEXT", nullable: true),
                    Usage = table.Column<string>(type: "TEXT", nullable: true),
                    Refs = table.Column<string>(type: "TEXT", nullable: false),
                    Topics = table.Column<string>(type: "TEXT", nullable: false),
                    MeaningGroupId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeaningItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeaningItems_MeaningGroups_MeaningGroupId",
                        column: x => x.MeaningGroupId,
                        principalTable: "MeaningGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Examples",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Text = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    MeaningItemId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Examples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Examples_MeaningItems_MeaningItemId",
                        column: x => x.MeaningItemId,
                        principalTable: "MeaningItems",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Examples_MeaningItemId",
                table: "Examples",
                column: "MeaningItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MeaningGroups_WordId",
                table: "MeaningGroups",
                column: "WordId");

            migrationBuilder.CreateIndex(
                name: "IX_MeaningItems_MeaningGroupId",
                table: "MeaningItems",
                column: "MeaningGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Pronunciations_WordId",
                table: "Pronunciations",
                column: "WordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Examples");

            migrationBuilder.DropTable(
                name: "Pronunciations");

            migrationBuilder.DropTable(
                name: "MeaningItems");

            migrationBuilder.DropTable(
                name: "MeaningGroups");

            migrationBuilder.DropTable(
                name: "Words");
        }
    }
}
