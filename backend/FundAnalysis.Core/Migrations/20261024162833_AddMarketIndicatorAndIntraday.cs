using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FundAnalysis.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketIndicatorAndIntraday : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IndexIntradays",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Time = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Price = table.Column<double>(type: "double precision", nullable: false),
                    Volume = table.Column<double>(type: "double precision", nullable: false),
                    Avg = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexIntradays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketIndicators",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Symbol = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    ChangePct = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketIndicators", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IndexIntradays_Code",
                table: "IndexIntradays",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_IndexIntradays_Code_Time",
                table: "IndexIntradays",
                columns: new[] { "Code", "Time" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketIndicators_Symbol",
                table: "MarketIndicators",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_MarketIndicators_Symbol_Date",
                table: "MarketIndicators",
                columns: new[] { "Symbol", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IndexIntradays");

            migrationBuilder.DropTable(
                name: "MarketIndicators");
        }
    }
}
