using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warhammer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignSwitching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CampaignPlayers",
                table: "CampaignPlayers");

            migrationBuilder.DropIndex(
                name: "IX_CampaignPlayers_CampaignId",
                table: "CampaignPlayers");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastOpenedUtc",
                table: "Campaigns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_CampaignPlayers",
                table: "CampaignPlayers",
                columns: new[] { "CampaignId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CampaignPlayers",
                table: "CampaignPlayers");

            migrationBuilder.DropColumn(
                name: "LastOpenedUtc",
                table: "Campaigns");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CampaignPlayers",
                table: "CampaignPlayers",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPlayers_CampaignId",
                table: "CampaignPlayers",
                column: "CampaignId");
        }
    }
}
