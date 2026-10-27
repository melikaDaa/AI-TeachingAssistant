using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.TeachingAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DurableJobQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GenerationJobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RequestKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LessonId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeacherId = table.Column<int>(type: "INTEGER", nullable: false),
                    SectionType = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FencingToken = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FailureCode = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenerationJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GenerationJobs_RequestKey",
                table: "GenerationJobs",
                column: "RequestKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GenerationJobs_Status_CreatedAtUtc",
                table: "GenerationJobs",
                columns: new[] { "Status", "CreatedAtUtc" });



        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GenerationJobs");
        }
    }
}
