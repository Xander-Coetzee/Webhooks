using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AcmeIntegration.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessingRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RecordsProcessed = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordsFailed = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessingErrors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProcessingRunId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceSystem = table.Column<string>(type: "TEXT", nullable: false),
                    ExternalOrderId = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessingErrors_ProcessingRuns_ProcessingRunId",
                        column: x => x.ProcessingRunId,
                        principalTable: "ProcessingRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingErrors_ProcessingRunId",
                table: "ProcessingErrors",
                column: "ProcessingRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessingErrors");

            migrationBuilder.DropTable(
                name: "ProcessingRuns");
        }
    }
}
