using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartEVCharging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFullNamePointsAndPortFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentPoints",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConnectorType",
                table: "ChargingPorts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PortNumber",
                table: "ChargingPorts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ChargingPorts",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentPoints",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ConnectorType",
                table: "ChargingPorts");

            migrationBuilder.DropColumn(
                name: "PortNumber",
                table: "ChargingPorts");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ChargingPorts");
        }
    }
}
