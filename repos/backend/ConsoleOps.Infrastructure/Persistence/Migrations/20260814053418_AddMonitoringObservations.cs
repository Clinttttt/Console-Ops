using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsoleOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitoringObservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "health_observations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    environment_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    response_milliseconds = table.Column<double>(type: "double precision", nullable: true),
                    observed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_health_observations", x => x.id);
                    table.ForeignKey(
                        name: "fk_health_observations_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "monitoring_activities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_monitoring_activities", x => x.id);
                    table.ForeignKey(
                        name: "fk_monitoring_activities_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "source_observations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_available = table.Column<bool>(type: "boolean", nullable: false),
                    repository = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: false),
                    default_branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    commit_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    short_commit_sha = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    committed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    observed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_source_observations", x => x.id);
                    table.ForeignKey(
                        name: "fk_source_observations_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "version_observations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    environment_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    application = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    version = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    commit_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    reported_environment = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    built_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    observed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_version_observations", x => x.id);
                    table.ForeignKey(
                        name: "fk_version_observations_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "version_sync_observations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    environment_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    commits_behind = table.Column<int>(type: "integer", nullable: true),
                    observed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_version_sync_observations", x => x.id);
                    table.ForeignKey(
                        name: "fk_version_sync_observations_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_observations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_file = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    workflow_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    commit_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    observed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_observations", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_observations_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dependency_health_observations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    health_observation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dependency_health_observations", x => x.id);
                    table.ForeignKey(
                        name: "fk_dependency_health_observations_health_observations_health_observation_id",
                        column: x => x.health_observation_id,
                        principalTable: "health_observations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dependency_health_observations_health_observation_id",
                table: "dependency_health_observations",
                column: "health_observation_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_observations_project_environment_observed_at",
                table: "health_observations",
                columns: new[] { "project_id", "environment_id", "observed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_monitoring_activities_project_occurred_at",
                table: "monitoring_activities",
                columns: new[] { "project_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_source_observations_project_observed_at",
                table: "source_observations",
                columns: new[] { "project_id", "observed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_version_observations_project_environment_observed_at",
                table: "version_observations",
                columns: new[] { "project_id", "environment_id", "observed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_version_sync_observations_project_environment_observed_at",
                table: "version_sync_observations",
                columns: new[] { "project_id", "environment_id", "observed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_observations_project_observed_at",
                table: "workflow_observations",
                columns: new[] { "project_id", "observed_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dependency_health_observations");

            migrationBuilder.DropTable(
                name: "monitoring_activities");

            migrationBuilder.DropTable(
                name: "source_observations");

            migrationBuilder.DropTable(
                name: "version_observations");

            migrationBuilder.DropTable(
                name: "version_sync_observations");

            migrationBuilder.DropTable(
                name: "workflow_observations");

            migrationBuilder.DropTable(
                name: "health_observations");
        }
    }
}
