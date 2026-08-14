using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsoleOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_projects_normalized_name",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "ux_projects_repository",
                table: "projects");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "archived_at_utc",
                table: "projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "configuration_version",
                table: "projects",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<bool>(
                name: "is_archived",
                table: "projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at_utc",
                table: "projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_projects_normalized_name",
                table: "projects",
                column: "normalized_name",
                unique: true,
                filter: "NOT is_archived");

            migrationBuilder.CreateIndex(
                name: "ux_projects_repository",
                table: "projects",
                columns: new[] { "normalized_repository_owner", "normalized_repository_name" },
                unique: true,
                filter: "NOT is_archived");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_projects_normalized_name",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "ux_projects_repository",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "archived_at_utc",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "configuration_version",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "is_archived",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "updated_at_utc",
                table: "projects");

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
    }
}
