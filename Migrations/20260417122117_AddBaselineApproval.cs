using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AccessEase12.Migrations
{
    /// <inheritdoc />
    public partial class AddBaselineApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BaselineApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ScanRecordId = table.Column<int>(type: "integer", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaselineApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BaselineApprovals_AppUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BaselineApprovals_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BaselineApprovals_ScanRecords_ScanRecordId",
                        column: x => x.ScanRecordId,
                        principalTable: "ScanRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BaselineApprovals_ApprovedByUserId",
                table: "BaselineApprovals",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BaselineApprovals_ProjectId_Url",
                table: "BaselineApprovals",
                columns: new[] { "ProjectId", "Url" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BaselineApprovals_ScanRecordId",
                table: "BaselineApprovals",
                column: "ScanRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BaselineApprovals");
        }
    }
}
