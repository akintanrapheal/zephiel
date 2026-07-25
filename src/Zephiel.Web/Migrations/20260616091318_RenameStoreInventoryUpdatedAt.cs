using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zephiel.Web.Migrations
{
    /// <inheritdoc />
    public partial class RenameStoreInventoryUpdatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LastSyncedAt",
                table: "StoreInventories",
                newName: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "StoreInventories",
                newName: "LastSyncedAt");
        }
    }
}
