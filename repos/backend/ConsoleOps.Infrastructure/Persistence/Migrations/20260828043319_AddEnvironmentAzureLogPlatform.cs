using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsoleOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEnvironmentAzureLogPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "azure_log_container_app_name",
                table: "project_environments",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "azure_log_platform",
                table: "project_environments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                defaultValue: "ContainerApp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "azure_log_platform",
                table: "project_environments");

            migrationBuilder.AlterColumn<string>(
                name: "azure_log_container_app_name",
                table: "project_environments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60,
                oldNullable: true);
        }
    }
}
