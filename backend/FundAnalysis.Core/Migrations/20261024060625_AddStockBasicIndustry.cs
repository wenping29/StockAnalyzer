using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FundAnalysis.Migrations
{
    /// <inheritdoc />
    public partial class AddStockBasicIndustry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Industry",
                table: "StockBasics",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Industry",
                table: "StockBasics");
        }
    }
}
