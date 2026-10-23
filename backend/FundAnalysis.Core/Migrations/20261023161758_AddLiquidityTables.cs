using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FundAnalysis.Migrations
{
    /// <inheritdoc />
    public partial class AddLiquidityTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LprRates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Lpr1Y = table.Column<double>(type: "REAL", nullable: false),
                    Lpr5Y = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LprRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MoneySupplies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Month = table.Column<DateTime>(type: "TEXT", nullable: false),
                    M2 = table.Column<double>(type: "REAL", nullable: false),
                    M1 = table.Column<double>(type: "REAL", nullable: false),
                    M0 = table.Column<double>(type: "REAL", nullable: false),
                    M2YoY = table.Column<double>(type: "REAL", nullable: false),
                    M1YoY = table.Column<double>(type: "REAL", nullable: false),
                    M0YoY = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoneySupplies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReserveRatios",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Large = table.Column<double>(type: "REAL", nullable: false),
                    Small = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReserveRatios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShiborRates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Period = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Rate = table.Column<double>(type: "REAL", nullable: false),
                    ChangeBp = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiborRates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LprRates_Date",
                table: "LprRates",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MoneySupplies_Month",
                table: "MoneySupplies",
                column: "Month",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReserveRatios_Date",
                table: "ReserveRatios",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiborRates_Date",
                table: "ShiborRates",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_ShiborRates_Period_Date",
                table: "ShiborRates",
                columns: new[] { "Period", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LprRates");

            migrationBuilder.DropTable(
                name: "MoneySupplies");

            migrationBuilder.DropTable(
                name: "ReserveRatios");

            migrationBuilder.DropTable(
                name: "ShiborRates");
        }
    }
}
