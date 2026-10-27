using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.TeachingAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UsageRateLimitMarker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRateLimitedRequest",
                table: "UsageEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRateLimitedRequest",
                table: "UsageEvents");
        }
    }
}
