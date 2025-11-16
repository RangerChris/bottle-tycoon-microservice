using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecyclerService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Recyclers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    CurrentLoad = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Location = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    LastEmptiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recyclers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Visitors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecyclerId = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitorType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Bottles = table.Column<int>(type: "integer", nullable: false),
                    ArrivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visitors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Visitors_Recyclers_RecyclerId",
                        column: x => x.RecyclerId,
                        principalTable: "Recyclers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Recyclers_CurrentLoad",
                table: "Recyclers",
                column: "CurrentLoad");

            migrationBuilder.CreateIndex(
                name: "IX_Visitors_RecyclerId",
                table: "Visitors",
                column: "RecyclerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Visitors");

            migrationBuilder.DropTable(
                name: "Recyclers");
        }
    }
}
