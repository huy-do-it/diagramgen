using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diagram.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class SyncSharedOutboxInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_PublishedAt",
                table: "OutboxMessages");

            migrationBuilder.AlterColumn<string>(
                name: "LastError",
                table: "OutboxMessages",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ProcessedAt",
                table: "InboxMessages",
                column: "ProcessedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InboxMessages_ProcessedAt",
                table: "InboxMessages");

            migrationBuilder.AlterColumn<string>(
                name: "LastError",
                table: "OutboxMessages",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PublishedAt",
                table: "OutboxMessages",
                column: "PublishedAt");
        }
    }
}
