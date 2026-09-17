using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartEVCharging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSensorReadingsAndRelayCommands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RelayCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PortNumber = table.Column<int>(type: "int", nullable: false),
                    Activate = table.Column<bool>(type: "bit", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    Consumed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RelayCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RelayCommands_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SensorReadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntranceDistanceCm = table.Column<double>(type: "float", nullable: true),
                    BinTopDistanceCm = table.Column<double>(type: "float", nullable: true),
                    BinBottomDistanceCm = table.Column<double>(type: "float", nullable: true),
                    BinFillPercentage = table.Column<int>(type: "int", nullable: false),
                    BottleDetectedAtEntrance = table.Column<bool>(type: "bit", nullable: false),
                    Sw1VoltageV = table.Column<double>(type: "float", nullable: true),
                    Sw1CurrentA = table.Column<double>(type: "float", nullable: true),
                    Sw2VoltageV = table.Column<double>(type: "float", nullable: true),
                    Sw2CurrentA = table.Column<double>(type: "float", nullable: true),
                    Sw3VoltageV = table.Column<double>(type: "float", nullable: true),
                    Sw3CurrentA = table.Column<double>(type: "float", nullable: true),
                    Sw4VoltageV = table.Column<double>(type: "float", nullable: true),
                    Sw4CurrentA = table.Column<double>(type: "float", nullable: true),
                    ConveyorRunning = table.Column<bool>(type: "bit", nullable: false),
                    ConveyorSpeedPwm = table.Column<int>(type: "int", nullable: false),
                    Relay1Active = table.Column<bool>(type: "bit", nullable: false),
                    Relay2Active = table.Column<bool>(type: "bit", nullable: false),
                    Relay3Active = table.Column<bool>(type: "bit", nullable: false),
                    Relay4Active = table.Column<bool>(type: "bit", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SensorReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SensorReadings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RelayCommands_DeviceId",
                table: "RelayCommands",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_SensorReadings_DeviceId",
                table: "SensorReadings",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RelayCommands");

            migrationBuilder.DropTable(
                name: "SensorReadings");
        }
    }
}
