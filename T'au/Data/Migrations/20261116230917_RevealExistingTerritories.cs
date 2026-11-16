using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warhammer.Data.Migrations
{
    /// <inheritdoc />
    public partial class RevealExistingTerritories : Migration
    {
        /// <inheritdoc />
        // Territories now hide until IsPublic is set. Ones that existed before were always shown,
        // so they start revealed; Territories created afterwards start hidden.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Nodes SET IsPublic = 1 WHERE NodeType = 'Territory';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: the previous per-row values were unused and are not recoverable.
        }
    }
}
