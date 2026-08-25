using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsoleOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperatorSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operator_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    github_user_id = table.Column<long>(type: "bigint", nullable: false),
                    login = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    avatar_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    protected_access_token = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    access_token_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    protected_refresh_token = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    refresh_token_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    signed_in_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operator_sessions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_operator_sessions_login",
                table: "operator_sessions",
                column: "login");

            migrationBuilder.CreateIndex(
                name: "ix_operator_sessions_refresh_expiry",
                table: "operator_sessions",
                column: "refresh_token_expires_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operator_sessions");
        }
    }
}
