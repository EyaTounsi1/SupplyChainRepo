using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PartTracker.Migrations
{
    /// <inheritdoc />
    public partial class UpdateModel20260218 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "safety_stock_items");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "safety_stock_items",
                columns: table => new
                {
                    part_number = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    mfg_supplier_code = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    safety_stock_nr_of_parts = table.Column<float>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_safety_stock_items", x => x.part_number);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
