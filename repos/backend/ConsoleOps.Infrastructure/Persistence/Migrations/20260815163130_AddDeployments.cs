using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsoleOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeployments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "deployments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_run_id = table.Column<long>(type: "bigint", nullable: false),
                    run_number = table.Column<int>(type: "integer", nullable: true),
                    workflow_file = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    workflow_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    commit_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    result = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    triggered_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    run_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    recorded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    observed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deployments", x => x.id);
                    table.ForeignKey(
                        name: "fk_deployments_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_deployments_project_commit_sha",
                table: "deployments",
                columns: new[] { "project_id", "commit_sha" });

            migrationBuilder.CreateIndex(
                name: "ix_deployments_project_external_run_id",
                table: "deployments",
                columns: new[] { "project_id", "external_run_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_deployments_started_at",
                table: "deployments",
                column: "started_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deployments");
        }
    }
}
