using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AccessEase12.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectUiValidationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectUiValidationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    RequireHeader = table.Column<bool>(type: "boolean", nullable: false),
                    RequireMain = table.Column<bool>(type: "boolean", nullable: false),
                    RequireFooter = table.Column<bool>(type: "boolean", nullable: false),
                    RequireNav = table.Column<bool>(type: "boolean", nullable: false),
                    RequirePageTitle = table.Column<bool>(type: "boolean", nullable: false),
                    RequireSingleH1 = table.Column<bool>(type: "boolean", nullable: false),
                    CheckUnlabeledInputs = table.Column<bool>(type: "boolean", nullable: false),
                    CheckDuplicateIds = table.Column<bool>(type: "boolean", nullable: false),
                    CheckHorizontalOverflow = table.Column<bool>(type: "boolean", nullable: false),
                    CheckTinyClickTargets = table.Column<bool>(type: "boolean", nullable: false),
                    MinClickTargetSize = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectUiValidationSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectUiValidationSettings_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUiValidationSettings_ProjectId",
                table: "ProjectUiValidationSettings",
                column: "ProjectId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectUiValidationSettings");
        }
    }
}
