using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.TeachingAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UsageReservationLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpiresAtUtc",
                table: "UsageEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentRequestKey",
                table: "UsageEvents",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeaseExpiresAtUtc",
                table: "UsageEvents");

            migrationBuilder.DropColumn(
                name: "ParentRequestKey",
                table: "UsageEvents");
        }
    }
}
