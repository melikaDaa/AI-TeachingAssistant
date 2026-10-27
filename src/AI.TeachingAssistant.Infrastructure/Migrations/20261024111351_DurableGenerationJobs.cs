using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.TeachingAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DurableGenerationJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CostStatus",
                table: "UsageEvents",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "UsageEvents",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutcomeDetail",
                table: "UsageEvents",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "UsageEvents",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProviderCostMicros",
                table: "UsageEvents",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostStatus",
                table: "UsageEvents");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "UsageEvents");

            migrationBuilder.DropColumn(
                name: "OutcomeDetail",
                table: "UsageEvents");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "UsageEvents");

            migrationBuilder.DropColumn(
                name: "ProviderCostMicros",
                table: "UsageEvents");
        }
    }
}
