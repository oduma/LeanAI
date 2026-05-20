using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeanAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_UseBmr_To_AppSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UseBmr",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UseBmr",
                table: "AppSettings");
        }
    }
}
