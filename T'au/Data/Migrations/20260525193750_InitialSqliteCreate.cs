using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warhammer.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqliteCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CampaignName = table.Column<string>(type: "TEXT", nullable: false),
                    MapSeed = table.Column<int>(type: "INTEGER", nullable: false),
                    MapBiome = table.Column<string>(type: "TEXT", nullable: false),
                    MapOctaves = table.Column<int>(type: "INTEGER", nullable: false),
                    MapLacunarity = table.Column<double>(type: "REAL", nullable: false),
                    MapPersistence = table.Column<double>(type: "REAL", nullable: false),
                    MapScale = table.Column<double>(type: "REAL", nullable: false),
                    MapOffsetX = table.Column<double>(type: "REAL", nullable: false),
                    MapOffsetY = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BattleRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<int>(type: "INTEGER", nullable: false),
                    DatePlayed = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MissionName = table.Column<string>(type: "TEXT", nullable: false),
                    WinnerPlayerId = table.Column<string>(type: "TEXT", nullable: false),
                    Player1Vp = table.Column<int>(type: "INTEGER", nullable: false),
                    Player2Vp = table.Column<int>(type: "INTEGER", nullable: false),
                    CapturedTerritoryId = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattleRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BattleRecords_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignPlayers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    VictoryPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    CampaignPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    Wins = table.Column<int>(type: "INTEGER", nullable: false),
                    Losses = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignPlayers_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GmNotes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    X = table.Column<double>(type: "REAL", nullable: false),
                    Y = table.Column<double>(type: "REAL", nullable: false),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GmNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GmNotes_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TerritoryNodes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    BuffEffect = table.Column<string>(type: "TEXT", nullable: false),
                    ControllingPlayerId = table.Column<string>(type: "TEXT", nullable: true),
                    ConnectedNodeIds = table.Column<string>(type: "TEXT", nullable: false),
                    SecretEventText = table.Column<string>(type: "TEXT", nullable: false),
                    IsEventRevealed = table.Column<bool>(type: "INTEGER", nullable: false),
                    X = table.Column<double>(type: "REAL", nullable: false),
                    Y = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerritoryNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TerritoryNodes_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BattleRecords_CampaignId",
                table: "BattleRecords",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPlayers_CampaignId",
                table: "CampaignPlayers",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_GmNotes_CampaignId",
                table: "GmNotes",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryNodes_CampaignId",
                table: "TerritoryNodes",
                column: "CampaignId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BattleRecords");

            migrationBuilder.DropTable(
                name: "CampaignPlayers");

            migrationBuilder.DropTable(
                name: "GmNotes");

            migrationBuilder.DropTable(
                name: "TerritoryNodes");

            migrationBuilder.DropTable(
                name: "Campaigns");
        }
    }
}
