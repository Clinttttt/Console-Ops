using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsoleOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEnvironmentAzureLogSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "azure_log_container_app_name",
                table: "project_environments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "azure_log_workspace_id",
                table: "project_environments",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "azure_log_container_app_name",
                table: "project_environments");

            migrationBuilder.DropColumn(
                name: "azure_log_workspace_id",
                table: "project_environments");
        }
    }
}
