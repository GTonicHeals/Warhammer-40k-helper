using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warhammer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGmArmyFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGmArmy",
                table: "OrdersOfBattle",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsGmArmy",
                table: "OrdersOfBattle");
        }
    }
}
