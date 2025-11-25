using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TruckId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecyclerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LoadByTypeJson = table.Column<string>(type: "TEXT", nullable: false),
                    GrossEarnings = table.Column<decimal>(type: "TEXT", nullable: false),
                    OperatingCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    NetProfit = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trucks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LicensePlate = table.Column<string>(type: "TEXT", nullable: false),
                    Model = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CapacityLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentLoadByTypeJson = table.Column<string>(type: "TEXT", nullable: false),
                    TotalEarnings = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trucks", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deliveries");

            migrationBuilder.DropTable(
                name: "trucks");
        }
    }
}
