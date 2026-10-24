using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FundAnalysis.Migrations
{
    /// <inheritdoc />
    public partial class AddOmoOperation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OmoOperations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    MaturityDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Term = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TermDays = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<double>(type: "double precision", nullable: false),
                    Rate = table.Column<double>(type: "double precision", nullable: true),
                    Announcement = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OmoOperations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OmoOperations_Date_Term",
                table: "OmoOperations",
                columns: new[] { "Date", "Term" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OmoOperations_MaturityDate",
                table: "OmoOperations",
                column: "MaturityDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OmoOperations");
        }
    }
}
