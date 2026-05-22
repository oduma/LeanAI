using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeanAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Refactor_DDD_Boundaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Rename CaloryLogs → EnergyLogs (data preserved)
            migrationBuilder.RenameTable(
                name: "CaloryLogs",
                newName: "EnergyLogs");

            // 2. Rebuild FoodLogs: rename CaloryLogId → EnergyLogId, drop FK + index
            migrationBuilder.Sql(@"
PRAGMA foreign_keys = OFF;

CREATE TABLE ""FoodLogs_new"" (
    ""Id""          TEXT NOT NULL CONSTRAINT ""PK_FoodLogs"" PRIMARY KEY,
    ""Date""        TEXT NOT NULL,
    ""FoodItem""    TEXT NOT NULL,
    ""Quantity""    TEXT NOT NULL,
    ""EnergyLogId"" TEXT NOT NULL
);

INSERT INTO ""FoodLogs_new"" (""Id"", ""Date"", ""FoodItem"", ""Quantity"", ""EnergyLogId"")
SELECT ""Id"", ""Date"", ""FoodItem"", ""Quantity"", ""CaloryLogId""
FROM ""FoodLogs"";

DROP TABLE ""FoodLogs"";
ALTER TABLE ""FoodLogs_new"" RENAME TO ""FoodLogs"";

PRAGMA foreign_keys = ON;
");

            // 3. Rebuild CustomActivityLogs: rename CaloryLogId → EnergyLogId, drop FK + index
            migrationBuilder.Sql(@"
PRAGMA foreign_keys = OFF;

CREATE TABLE ""CustomActivityLogs_new"" (
    ""Id""          TEXT NOT NULL CONSTRAINT ""PK_CustomActivityLogs"" PRIMARY KEY,
    ""Date""        TEXT NOT NULL,
    ""Description"" TEXT NOT NULL,
    ""EnergyLogId"" TEXT NOT NULL
);

INSERT INTO ""CustomActivityLogs_new"" (""Id"", ""Date"", ""Description"", ""EnergyLogId"")
SELECT ""Id"", ""Date"", ""Description"", ""CaloryLogId""
FROM ""CustomActivityLogs"";

DROP TABLE ""CustomActivityLogs"";
ALTER TABLE ""CustomActivityLogs_new"" RENAME TO ""CustomActivityLogs"";

PRAGMA foreign_keys = ON;
");

            // 4. Create RoutineFoodItems
            migrationBuilder.CreateTable(
                name: "RoutineFoodItems",
                columns: table => new
                {
                    Id          = table.Column<Guid>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity    = table.Column<string>(type: "TEXT", nullable: true),
                    Calories    = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutineFoodItems", x => x.Id);
                });

            // 5. Create RoutineActivityItems
            migrationBuilder.CreateTable(
                name: "RoutineActivityItems",
                columns: table => new
                {
                    Id          = table.Column<Guid>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Calories    = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutineActivityItems", x => x.Id);
                });

            // 6. Migrate data from RoutineItems
            migrationBuilder.Sql(@"
INSERT INTO ""RoutineFoodItems"" (""Id"", ""Description"", ""Quantity"", ""Calories"")
SELECT ""Id"", ""Description"", ""Quantity"", ""Calories""
FROM ""RoutineItems""
WHERE ""SourceType"" = 'food';

INSERT INTO ""RoutineActivityItems"" (""Id"", ""Description"", ""Calories"")
SELECT ""Id"", ""Description"", ""Calories""
FROM ""RoutineItems""
WHERE ""SourceType"" = 'activity';
");

            // 7. Drop RoutineItems
            migrationBuilder.DropTable(name: "RoutineItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: recreate RoutineItems from the two typed tables
            migrationBuilder.CreateTable(
                name: "RoutineItems",
                columns: table => new
                {
                    Id          = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceType  = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity    = table.Column<string>(type: "TEXT", nullable: true),
                    Calories    = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutineItems", x => x.Id);
                });

            migrationBuilder.Sql(@"
INSERT INTO ""RoutineItems"" (""Id"", ""SourceType"", ""Description"", ""Quantity"", ""Calories"")
SELECT ""Id"", 'food', ""Description"", ""Quantity"", ""Calories""
FROM ""RoutineFoodItems"";

INSERT INTO ""RoutineItems"" (""Id"", ""SourceType"", ""Description"", ""Calories"")
SELECT ""Id"", 'activity', ""Description"", ""Calories""
FROM ""RoutineActivityItems"";
");

            migrationBuilder.DropTable(name: "RoutineFoodItems");
            migrationBuilder.DropTable(name: "RoutineActivityItems");

            // Reverse FoodLogs rebuild
            migrationBuilder.Sql(@"
PRAGMA foreign_keys = OFF;

CREATE TABLE ""FoodLogs_old"" (
    ""Id""          TEXT NOT NULL CONSTRAINT ""PK_FoodLogs"" PRIMARY KEY,
    ""Date""        TEXT NOT NULL,
    ""FoodItem""    TEXT NOT NULL,
    ""Quantity""    TEXT NOT NULL,
    ""CaloryLogId"" TEXT NOT NULL,
    CONSTRAINT ""FK_FoodLogs_CaloryLogs_CaloryLogId"" FOREIGN KEY (""CaloryLogId"")
        REFERENCES ""CaloryLogs"" (""Id"") ON DELETE CASCADE
);

INSERT INTO ""FoodLogs_old"" (""Id"", ""Date"", ""FoodItem"", ""Quantity"", ""CaloryLogId"")
SELECT ""Id"", ""Date"", ""FoodItem"", ""Quantity"", ""EnergyLogId""
FROM ""FoodLogs"";

DROP TABLE ""FoodLogs"";
ALTER TABLE ""FoodLogs_old"" RENAME TO ""FoodLogs"";

PRAGMA foreign_keys = ON;
");

            // Reverse CustomActivityLogs rebuild
            migrationBuilder.Sql(@"
PRAGMA foreign_keys = OFF;

CREATE TABLE ""CustomActivityLogs_old"" (
    ""Id""          TEXT NOT NULL CONSTRAINT ""PK_CustomActivityLogs"" PRIMARY KEY,
    ""Date""        TEXT NOT NULL,
    ""Description"" TEXT NOT NULL,
    ""CaloryLogId"" TEXT NOT NULL,
    CONSTRAINT ""FK_CustomActivityLogs_CaloryLogs_CaloryLogId"" FOREIGN KEY (""CaloryLogId"")
        REFERENCES ""CaloryLogs"" (""Id"") ON DELETE CASCADE
);

INSERT INTO ""CustomActivityLogs_old"" (""Id"", ""Date"", ""Description"", ""CaloryLogId"")
SELECT ""Id"", ""Date"", ""Description"", ""EnergyLogId""
FROM ""CustomActivityLogs"";

DROP TABLE ""CustomActivityLogs"";
ALTER TABLE ""CustomActivityLogs_old"" RENAME TO ""CustomActivityLogs"";

PRAGMA foreign_keys = ON;
");

            // Reverse EnergyLogs → CaloryLogs
            migrationBuilder.RenameTable(
                name: "EnergyLogs",
                newName: "CaloryLogs");
        }
    }
}
