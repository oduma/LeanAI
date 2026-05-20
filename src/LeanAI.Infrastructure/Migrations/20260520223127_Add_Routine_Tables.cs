using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeanAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Routine_Tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RoutineItemId",
                table: "CaloryLogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DailyRoutineStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyRoutineStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoutineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<string>(type: "TEXT", nullable: true),
                    Calories = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutineItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyRoutineStatuses_Date",
                table: "DailyRoutineStatuses",
                column: "Date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyRoutineStatuses");

            migrationBuilder.DropTable(
                name: "RoutineItems");

            migrationBuilder.DropColumn(
                name: "RoutineItemId",
                table: "CaloryLogs");
        }
    }
}
