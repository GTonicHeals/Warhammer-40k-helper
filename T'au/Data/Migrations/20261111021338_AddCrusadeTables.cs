using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warhammer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCrusadeTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrdersOfBattle",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<int>(type: "INTEGER", nullable: false),
                    ArmyName = table.Column<string>(type: "TEXT", nullable: false),
                    Faction = table.Column<string>(type: "TEXT", nullable: false),
                    DetachmentName = table.Column<string>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<string>(type: "TEXT", nullable: false),
                    SupplyLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    RequisitionPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomTraits = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdersOfBattle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdersOfBattle_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CrusadeUnitCards",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    OrderOfBattleId = table.Column<string>(type: "TEXT", nullable: false),
                    UnitName = table.Column<string>(type: "TEXT", nullable: false),
                    DatasheetName = table.Column<string>(type: "TEXT", nullable: false),
                    TotalPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    Xp = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitsDestroyed = table.Column<int>(type: "INTEGER", nullable: false),
                    BattlesPlayed = table.Column<int>(type: "INTEGER", nullable: false),
                    BattlesSurvived = table.Column<int>(type: "INTEGER", nullable: false),
                    IsTitanic = table.Column<bool>(type: "INTEGER", nullable: false),
                    XpEligible = table.Column<bool>(type: "INTEGER", nullable: false),
                    BattleHonours = table.Column<string>(type: "TEXT", nullable: false),
                    BattleScars = table.Column<string>(type: "TEXT", nullable: false),
                    IsCharacter = table.Column<bool>(type: "INTEGER", nullable: false),
                    LegendaryVeteransUsed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrusadeUnitCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrusadeUnitCards_OrdersOfBattle_OrderOfBattleId",
                        column: x => x.OrderOfBattleId,
                        principalTable: "OrdersOfBattle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrusadeUnitCards_OrderOfBattleId",
                table: "CrusadeUnitCards",
                column: "OrderOfBattleId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdersOfBattle_CampaignId",
                table: "OrdersOfBattle",
                column: "CampaignId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrusadeUnitCards");

            migrationBuilder.DropTable(
                name: "OrdersOfBattle");
        }
    }
}
