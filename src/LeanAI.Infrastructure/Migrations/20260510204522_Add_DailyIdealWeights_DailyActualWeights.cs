using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeanAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_DailyIdealWeights_DailyActualWeights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyActualWeights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<string>(type: "TEXT", nullable: false),
                    WeightKg = table.Column<double>(type: "REAL", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyActualWeights", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyIdealWeights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<string>(type: "TEXT", nullable: false),
                    WeightKg = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyIdealWeights", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyActualWeights");

            migrationBuilder.DropTable(
                name: "DailyIdealWeights");
        }
    }
}
