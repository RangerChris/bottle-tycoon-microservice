using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckService.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trucks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LicensePlate = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    CapacityLevel = table.Column<int>(type: "integer", nullable: false),
                    CurrentLoadByTypeJson = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    TotalEarnings = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trucks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TruckId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecyclerId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    GrossEarnings = table.Column<decimal>(type: "numeric", nullable: false),
                    NetProfit = table.Column<decimal>(type: "numeric", nullable: false),
                    OperatingCost = table.Column<decimal>(type: "numeric", nullable: false),
                    LoadByTypeJson = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deliveries", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "deliveries");
            migrationBuilder.DropTable(name: "trucks");
        }
    }
}