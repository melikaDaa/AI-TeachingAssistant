using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.TeachingAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Teachers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Level = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Grade = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Subjects = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TeachingStyle = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teachers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lessons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeacherId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Grade = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Topic = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    DurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    StudentLevel = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AdditionalInstructions = table.Column<string>(type: "TEXT", nullable: true),
                    PackageStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RequestedSections = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lessons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lessons_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeachingPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LessonId = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedSections = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeachingPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeachingPackages_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassroomActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeachingPackageId = table.Column<int>(type: "INTEGER", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassroomActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassroomActivities_TeachingPackages_TeachingPackageId",
                        column: x => x.TeachingPackageId,
                        principalTable: "TeachingPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EducationalContents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeachingPackageId = table.Column<int>(type: "INTEGER", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EducationalContents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EducationalContents_TeachingPackages_TeachingPackageId",
                        column: x => x.TeachingPackageId,
                        principalTable: "TeachingPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LessonPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeachingPackageId = table.Column<int>(type: "INTEGER", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonPlans_TeachingPackages_TeachingPackageId",
                        column: x => x.TeachingPackageId,
                        principalTable: "TeachingPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaPrompts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeachingPackageId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaPrompts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaPrompts_TeachingPackages_TeachingPackageId",
                        column: x => x.TeachingPackageId,
                        principalTable: "TeachingPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Quizzes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeachingPackageId = table.Column<int>(type: "INTEGER", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quizzes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quizzes_TeachingPackages_TeachingPackageId",
                        column: x => x.TeachingPackageId,
                        principalTable: "TeachingPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomActivities_TeachingPackageId",
                table: "ClassroomActivities",
                column: "TeachingPackageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EducationalContents_TeachingPackageId",
                table: "EducationalContents",
                column: "TeachingPackageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LessonPlans_TeachingPackageId",
                table: "LessonPlans",
                column: "TeachingPackageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_TeacherId_CreatedAt",
                table: "Lessons",
                columns: new[] { "TeacherId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaPrompts_TeachingPackageId",
                table: "MediaPrompts",
                column: "TeachingPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_Quizzes_TeachingPackageId",
                table: "Quizzes",
                column: "TeachingPackageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeachingPackages_LessonId",
                table: "TeachingPackages",
                column: "LessonId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassroomActivities");

            migrationBuilder.DropTable(
                name: "EducationalContents");

            migrationBuilder.DropTable(
                name: "LessonPlans");

            migrationBuilder.DropTable(
                name: "MediaPrompts");

            migrationBuilder.DropTable(
                name: "Quizzes");

            migrationBuilder.DropTable(
                name: "TeachingPackages");

            migrationBuilder.DropTable(
                name: "Lessons");

            migrationBuilder.DropTable(
                name: "Teachers");
        }
    }
}
