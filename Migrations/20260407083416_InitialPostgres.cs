using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AccessEase12.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScanRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Url = table.Column<string>(type: "text", nullable: false),
                    ScanTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssueCount = table.Column<int>(type: "integer", nullable: false),
                    IssuesJson = table.Column<string>(type: "text", nullable: false),
                    AppUserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanRecords_AppUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AxeIssues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Impact = table.Column<string>(type: "text", nullable: false),
                    Rule = table.Column<string>(type: "text", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    HelpUrl = table.Column<string>(type: "text", nullable: false),
                    Help = table.Column<string>(type: "text", nullable: false),
                    Target = table.Column<string>(type: "text", nullable: false),
                    FixTip = table.Column<string>(type: "text", nullable: false),
                    HtmlSnippet = table.Column<string>(type: "text", nullable: false),
                    EvidenceUrl = table.Column<string>(type: "text", nullable: false),
                    ScreenshotUrl = table.Column<string>(type: "text", nullable: false),
                    ScanId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AxeIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AxeIssues_ScanRecords_ScanId",
                        column: x => x.ScanId,
                        principalTable: "ScanRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_Email",
                table: "AppUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AxeIssues_ScanId",
                table: "AxeIssues",
                column: "ScanId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanRecords_AppUserId",
                table: "ScanRecords",
                column: "AppUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AxeIssues");

            migrationBuilder.DropTable(
                name: "ScanRecords");

            migrationBuilder.DropTable(
                name: "AppUsers");
        }
    }
}
