using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessEase12.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultUrlToProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultUrl",
                table: "Projects",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultUrl",
                table: "Projects");
        }
    }
}
