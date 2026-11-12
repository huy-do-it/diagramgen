using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UMLForge.Render.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class InitRender : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RenderDailyReports",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Total = table.Column<int>(type: "int", nullable: false),
                    Done = table.Column<int>(type: "int", nullable: false),
                    Failed = table.Column<int>(type: "int", nullable: false),
                    AvgMs = table.Column<double>(type: "float", nullable: false),
                    SuccessRate = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenderDailyReports", x => x.Date);
                });

            migrationBuilder.CreateTable(
                name: "RenderJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiagramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiagramVersion = table.Column<int>(type: "int", nullable: false),
                    DiagramType = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ModelJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenderJobs", x => x.Id);
                    table.CheckConstraint("CK_RenderJobs_Attempt", "[Attempt] >= 0");
                    table.CheckConstraint("CK_RenderJobs_Type", "[DiagramType] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_RenderJobs_Version", "[DiagramVersion] > 0");
                });

            migrationBuilder.CreateTable(
                name: "RenderArtifacts",
                columns: table => new
                {
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlantUml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SvgXml = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenderArtifacts", x => x.JobId);
                    table.ForeignKey(
                        name: "FK_RenderArtifacts_RenderJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "RenderJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RenderJobs_DiagramId_DiagramVersion",
                table: "RenderJobs",
                columns: new[] { "DiagramId", "DiagramVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RenderJobs_Status_CreatedAt",
                table: "RenderJobs",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RenderArtifacts");

            migrationBuilder.DropTable(
                name: "RenderDailyReports");

            migrationBuilder.DropTable(
                name: "RenderJobs");
        }
    }
}
