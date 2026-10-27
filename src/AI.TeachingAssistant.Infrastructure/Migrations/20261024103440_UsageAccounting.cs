using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.TeachingAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UsageAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UsageEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeacherId = table.Column<int>(type: "INTEGER", nullable: false),
                    Operation = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    RequestKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Units = table.Column<int>(type: "INTEGER", nullable: false),
                    PromptTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletionTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    ImageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Succeeded = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsReservation = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsageEvents_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_RequestKey",
                table: "UsageEvents",
                column: "RequestKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_TeacherId_CreatedAtUtc",
                table: "UsageEvents",
                columns: new[] { "TeacherId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UsageEvents");
        }
    }
}
