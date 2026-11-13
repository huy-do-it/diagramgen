using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diagram.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddDiagramVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiagramVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiagramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ModelJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagramVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiagramVersions_Diagrams_DiagramId",
                        column: x => x.DiagramId,
                        principalTable: "Diagrams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiagramVersions_DiagramId",
                table: "DiagramVersions",
                column: "DiagramId");

            migrationBuilder.CreateIndex(
                name: "IX_DiagramVersions_DiagramId_Version",
                table: "DiagramVersions",
                columns: new[] { "DiagramId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiagramVersions");
        }
    }
}
