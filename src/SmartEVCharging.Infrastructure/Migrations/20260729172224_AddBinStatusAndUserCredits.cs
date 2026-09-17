using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartEVCharging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBinStatusAndUserCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BottlesDepositedTotal",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CreditsSeconds",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BinStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FillPercentage = table.Column<int>(type: "int", nullable: false),
                    TotalBottlesCollected = table.Column<int>(type: "int", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BinStatuses", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BinStatuses");

            migrationBuilder.DropColumn(
                name: "BottlesDepositedTotal",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreditsSeconds",
                table: "Users");
        }
    }
}
