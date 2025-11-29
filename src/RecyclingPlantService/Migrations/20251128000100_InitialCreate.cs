using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecyclingPlantService.Data.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plant_deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TruckId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    GlassCount = table.Column<int>(type: "integer", nullable: false),
                    MetalCount = table.Column<int>(type: "integer", nullable: false),
                    PlasticCount = table.Column<int>(type: "integer", nullable: false),
                    GrossEarnings = table.Column<decimal>(type: "numeric", nullable: false),
                    OperatingCost = table.Column<decimal>(type: "numeric", nullable: false),
                    NetEarnings = table.Column<decimal>(type: "numeric", nullable: false),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plant_deliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "player_earnings",
                columns: table => new
                {
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalEarnings = table.Column<decimal>(type: "numeric", nullable: false),
                    DeliveryCount = table.Column<int>(type: "integer", nullable: false),
                    AverageEarnings = table.Column<decimal>(type: "numeric", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_earnings", x => x.PlayerId);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "plant_deliveries");
            migrationBuilder.DropTable(name: "player_earnings");
        }
    }
}