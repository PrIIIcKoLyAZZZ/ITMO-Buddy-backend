using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITMO.Buddy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountFileCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL supplies the refresh token xmin column implicitly.
            migrationBuilder.CreateTable(
                name: "file_cleanup_queue",
                columns: table => new
                {
                    object_key = table.Column<string>(type: "text", nullable: false),
                    enqueued_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_cleanup_queue", x => x.object_key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "file_cleanup_queue");
        }
    }
}
