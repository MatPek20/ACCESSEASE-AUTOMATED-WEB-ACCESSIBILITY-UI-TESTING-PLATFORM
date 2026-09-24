using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AccessEase12.Migrations
{
    /// <inheritdoc />
    public partial class AddRemediationTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RemediationIssues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScanRecordId = table.Column<int>(type: "integer", nullable: false),
                    Impact = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Rule = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Target = table.Column<string>(type: "text", nullable: true),
                    FixTip = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemediationIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemediationIssues_ScanRecords_ScanRecordId",
                        column: x => x.ScanRecordId,
                        principalTable: "ScanRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemediationIssues_ScanRecordId",
                table: "RemediationIssues",
                column: "ScanRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RemediationIssues");
        }
    }
}
