using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PartTracker.Migrations
{
    /// <inheritdoc />
    public partial class UpdateChangeLogEntryModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "aftermarket_collection_or_speed_up",
                table: "change_log_entries");

            migrationBuilder.DropColumn(
                name: "reasons3",
                table: "change_log_entries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "aftermarket_collection_or_speed_up",
                table: "change_log_entries",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "reasons3",
                table: "change_log_entries",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
