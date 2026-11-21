using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warhammer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorldSurfaceControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "MapFeatures",
                table: "Campaigns",
                type: "REAL",
                nullable: false,
                defaultValue: 0.5);

            migrationBuilder.AddColumn<string>(
                name: "MapLandmass",
                table: "Campaigns",
                type: "TEXT",
                nullable: false,
                defaultValue: "continent");

            migrationBuilder.AddColumn<int>(
                name: "MapRivers",
                table: "Campaigns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<double>(
                name: "MapRuggedness",
                table: "Campaigns",
                type: "REAL",
                nullable: false,
                defaultValue: 0.5);

            migrationBuilder.AddColumn<double>(
                name: "MapSeaLevel",
                table: "Campaigns",
                type: "REAL",
                nullable: false,
                defaultValue: 0.35);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MapFeatures",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "MapLandmass",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "MapRivers",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "MapRuggedness",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "MapSeaLevel",
                table: "Campaigns");
        }
    }
}
