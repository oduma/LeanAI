using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeanAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_CustomActivityLogAndCaloryLogDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "CaloryLogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomActivityLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    CaloryLogId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomActivityLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomActivityLogs_CaloryLogs_CaloryLogId",
                        column: x => x.CaloryLogId,
                        principalTable: "CaloryLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomActivityLogs_CaloryLogId",
                table: "CustomActivityLogs",
                column: "CaloryLogId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomActivityLogs");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "CaloryLogs");
        }
    }
}
