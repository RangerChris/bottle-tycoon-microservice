using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    credits = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_players", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purchases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    playerid = table.Column<Guid>(type: "uuid", nullable: false),
                    itemtype = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    purchasedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchases", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchases_players_playerid",
                        column: x => x.playerid,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "upgrades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    playerid = table.Column<Guid>(type: "uuid", nullable: false),
                    itemtype = table.Column<string>(type: "text", nullable: false),
                    itemid = table.Column<int>(type: "integer", nullable: false),
                    newlevel = table.Column<int>(type: "integer", nullable: false),
                    cost = table.Column<decimal>(type: "numeric", nullable: false),
                    upgradedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_upgrades", x => x.id);
                    table.ForeignKey(
                        name: "fk_upgrades_players_playerid",
                        column: x => x.playerid,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_purchases_playerid",
                table: "purchases",
                column: "playerid");

            migrationBuilder.CreateIndex(
                name: "ix_upgrades_playerid",
                table: "upgrades",
                column: "playerid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchases");

            migrationBuilder.DropTable(
                name: "upgrades");

            migrationBuilder.DropTable(
                name: "players");
        }
    }
}
