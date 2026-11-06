using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FundAnalysis.Migrations
{
    /// <inheritdoc />
    public partial class AddFundHoldings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FundHoldings",
                columns: table => new
                {
                    FundCode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    StockSymbol = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    StockName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SecMarket = table.Column<int>(type: "integer", nullable: false),
                    WeightPct = table.Column<double>(type: "double precision", nullable: false),
                    ReportDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundHoldings", x => new { x.FundCode, x.Rank });
                });

            migrationBuilder.CreateTable(
                name: "FundHoldingSyncs",
                columns: table => new
                {
                    FundCode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReportDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    HoldingCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundHoldingSyncs", x => x.FundCode);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FundHoldings");

            migrationBuilder.DropTable(
                name: "FundHoldingSyncs");
        }
    }
}
