using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warhammer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBattleMuster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Defaults backfill rows logged before the Muster existed: those battles were
            // entered after they were played, so they are Completed, with no unit results.
            migrationBuilder.AddColumn<string>(
                name: "Player1Id",
                table: "BattleRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "p1");

            migrationBuilder.AddColumn<string>(
                name: "Player1OrderId",
                table: "BattleRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Player2Id",
                table: "BattleRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "p2");

            migrationBuilder.AddColumn<string>(
                name: "Player2OrderId",
                table: "BattleRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointsLimit",
                table: "BattleRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "BattleRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "UnitResults",
                table: "BattleRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Player1Id",
                table: "BattleRecords");

            migrationBuilder.DropColumn(
                name: "Player1OrderId",
                table: "BattleRecords");

            migrationBuilder.DropColumn(
                name: "Player2Id",
                table: "BattleRecords");

            migrationBuilder.DropColumn(
                name: "Player2OrderId",
                table: "BattleRecords");

            migrationBuilder.DropColumn(
                name: "PointsLimit",
                table: "BattleRecords");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "BattleRecords");

            migrationBuilder.DropColumn(
                name: "UnitResults",
                table: "BattleRecords");
        }
    }
}
