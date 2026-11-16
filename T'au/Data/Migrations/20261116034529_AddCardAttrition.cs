using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warhammer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCardAttrition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ModelCount",
                table: "CrusadeUnitCards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ModelsLost",
                table: "CrusadeUnitCards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WoundsLost",
                table: "CrusadeUnitCards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WoundsPerModel",
                table: "CrusadeUnitCards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModelCount",
                table: "CrusadeUnitCards");

            migrationBuilder.DropColumn(
                name: "ModelsLost",
                table: "CrusadeUnitCards");

            migrationBuilder.DropColumn(
                name: "WoundsLost",
                table: "CrusadeUnitCards");

            migrationBuilder.DropColumn(
                name: "WoundsPerModel",
                table: "CrusadeUnitCards");
        }
    }
}
