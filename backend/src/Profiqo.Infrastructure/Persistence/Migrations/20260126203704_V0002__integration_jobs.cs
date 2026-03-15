using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Profiqo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V0002__integration_jobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "integration_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    page_size = table.Column<int>(type: "integer", nullable: false),
                    max_pages = table.Column<int>(type: "integer", nullable: false),
                    processed_items = table.Column<int>(type: "integer", nullable: false),
                    locked_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    locked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integration_jobs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_integration_jobs_batch_id",
                table: "integration_jobs",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_integration_jobs_status_created_at_utc",
                table: "integration_jobs",
                columns: new[] { "status", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_integration_jobs_tenant_id_connection_id",
                table: "integration_jobs",
                columns: new[] { "tenant_id", "connection_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_jobs");
        }
    }
}
