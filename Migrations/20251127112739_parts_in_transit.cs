using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PartTracker.Migrations
{
    /// <inheritdoc />
    public partial class parts_in_transit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "parts_in_transit",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    plan_pkt = table.Column<int>(type: "int", nullable: false),
                    lev_nr = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    art_nr = table.Column<int>(type: "int", nullable: false),
                    fs_nr = table.Column<int>(type: "int", nullable: false),
                    avs_dat = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    fav_tid = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    fav_artan = table.Column<int>(type: "int", nullable: false),
                    avi_tid = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    avi_artan = table.Column<int>(type: "int", nullable: false),
                    mot_tid = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    mot_antal = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_parts_in_transit", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parts_in_transit");
        }
    }
}
