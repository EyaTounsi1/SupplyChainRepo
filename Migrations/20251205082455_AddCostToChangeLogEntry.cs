using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PartTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddCostToChangeLogEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "cost",
                table: "change_log_entries",
                type: "decimal(65,30)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cost",
                table: "change_log_entries");
        }
    }
}
