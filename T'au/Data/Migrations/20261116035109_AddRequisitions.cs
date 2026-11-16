using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warhammer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRequisitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequisitionLog",
                table: "OrdersOfBattle",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Requisitions",
                table: "Campaigns",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<bool>(
                name: "RequisitionsSeeded",
                table: "Campaigns",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequisitionLog",
                table: "OrdersOfBattle");

            migrationBuilder.DropColumn(
                name: "Requisitions",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "RequisitionsSeeded",
                table: "Campaigns");
        }
    }
}
