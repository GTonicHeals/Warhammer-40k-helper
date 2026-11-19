using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warhammer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGmNoteTableMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsInbox",
                table: "GmNotes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsStarred",
                table: "GmNotes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NodeId",
                table: "GmNotes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevealedAt",
                table: "GmNotes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Secret",
                table: "GmNotes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "GmNotes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsInbox",
                table: "GmNotes");

            migrationBuilder.DropColumn(
                name: "IsStarred",
                table: "GmNotes");

            migrationBuilder.DropColumn(
                name: "NodeId",
                table: "GmNotes");

            migrationBuilder.DropColumn(
                name: "RevealedAt",
                table: "GmNotes");

            migrationBuilder.DropColumn(
                name: "Secret",
                table: "GmNotes");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "GmNotes");
        }
    }
}
