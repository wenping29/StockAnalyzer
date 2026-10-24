using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FundAnalysis.Migrations
{
    /// <inheritdoc />
    public partial class AddStockFundamental : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockFundamentals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Symbol = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Industry = table.Column<string>(type: "TEXT", nullable: true),
                    ClosePrice = table.Column<double>(type: "REAL", nullable: false),
                    PeTtm = table.Column<double>(type: "REAL", nullable: true),
                    PbMrq = table.Column<double>(type: "REAL", nullable: true),
                    PsTtm = table.Column<double>(type: "REAL", nullable: true),
                    MarketCap = table.Column<double>(type: "REAL", nullable: true),
                    ReportDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Revenue = table.Column<double>(type: "REAL", nullable: false),
                    RevenueYoY = table.Column<double>(type: "REAL", nullable: true),
                    NetProfit = table.Column<double>(type: "REAL", nullable: false),
                    NetProfitYoY = table.Column<double>(type: "REAL", nullable: true),
                    Roe = table.Column<double>(type: "REAL", nullable: true),
                    GrossMargin = table.Column<double>(type: "REAL", nullable: true),
                    NetMargin = table.Column<double>(type: "REAL", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockFundamentals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockFundamentals_Industry",
                table: "StockFundamentals",
                column: "Industry");

            migrationBuilder.CreateIndex(
                name: "IX_StockFundamentals_Symbol",
                table: "StockFundamentals",
                column: "Symbol",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockFundamentals");
        }
    }
}
