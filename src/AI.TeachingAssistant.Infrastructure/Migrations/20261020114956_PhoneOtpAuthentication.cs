using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.TeachingAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PhoneOtpAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Teachers",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Teachers",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PhoneVerificationChallenges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CodeHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FailedAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestIpHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsRegistration = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhoneVerificationChallenges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_PhoneNumber",
                table: "Teachers",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhoneVerificationChallenges_PhoneNumber",
                table: "PhoneVerificationChallenges",
                column: "PhoneNumber",
                unique: true,
                filter: "\"ConsumedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneVerificationChallenges_PhoneNumber_CreatedAt",
                table: "PhoneVerificationChallenges",
                columns: new[] { "PhoneNumber", "CreatedAt" });

            // Keep existing data intact but block login until ownership is explicitly resolved.
            migrationBuilder.Sql("UPDATE \"Teachers\" SET \"IsActive\" = 0 WHERE \"PhoneNumber\" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhoneVerificationChallenges");

            migrationBuilder.DropIndex(
                name: "IX_Teachers_PhoneNumber",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Teachers");
        }
    }
}
