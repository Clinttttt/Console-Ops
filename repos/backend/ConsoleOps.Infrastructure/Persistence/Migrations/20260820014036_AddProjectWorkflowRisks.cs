using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsoleOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectWorkflowRisks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_workflow_risks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_path = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    normalized_workflow_path = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_workflow_risks", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_workflow_risks_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_project_workflow_risks_project_path",
                table: "project_workflow_risks",
                columns: new[] { "project_id", "normalized_workflow_path" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_workflow_risks");
        }
    }
}
