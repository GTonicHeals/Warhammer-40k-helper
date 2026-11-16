using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warhammer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBattleRequisitionAwards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Player1RpAwarded",
                table: "BattleRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Player2RpAwarded",
                table: "BattleRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Player1RpAwarded",
                table: "BattleRecords");

            migrationBuilder.DropColumn(
                name: "Player2RpAwarded",
                table: "BattleRecords");
        }
    }
}
