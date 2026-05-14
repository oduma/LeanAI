using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeanAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_WeeklyAverages_And_CalendarFirstDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CalendarFirstDay",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1); // 1 = DayOfWeek.Monday

            migrationBuilder.CreateTable(
                name: "WeeklyAverages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WeekStart = table.Column<string>(type: "TEXT", nullable: false),
                    AverageWeightKg = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyAverages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyAverages_WeekStart",
                table: "WeeklyAverages",
                column: "WeekStart",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeeklyAverages");

            migrationBuilder.DropColumn(
                name: "CalendarFirstDay",
                table: "AppSettings");
        }
    }
}
