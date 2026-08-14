using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsoleOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    repository_owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_repository_owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    repository_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_repository_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    default_branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    workflow_file = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_projects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "project_environments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    application_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    health_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    version_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_environments", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_environments_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_project_environments_project_name",
                table: "project_environments",
                columns: new[] { "project_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_projects_normalized_name",
                table: "projects",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_projects_repository",
                table: "projects",
                columns: new[] { "normalized_repository_owner", "normalized_repository_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_environments");

            migrationBuilder.DropTable(
                name: "projects");
        }
    }
}
