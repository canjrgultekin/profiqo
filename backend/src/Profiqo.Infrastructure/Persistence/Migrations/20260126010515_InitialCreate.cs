using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Profiqo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "automation_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    trigger_type = table.Column<short>(type: "smallint", nullable: false),
                    trigger_event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    trigger_cron = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    trigger_score_field = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    trigger_score_operator = table.Column<short>(type: "smallint", nullable: true),
                    trigger_score_value_json = table.Column<string>(type: "jsonb", nullable: true),
                    delay_value = table.Column<int>(type: "integer", nullable: false),
                    delay_unit = table.Column<short>(type: "smallint", nullable: false),
                    limit_max_per_day = table.Column<int>(type: "integer", nullable: false),
                    limit_max_total = table.Column<int>(type: "integer", nullable: false),
                    limit_cooldown_hours = table.Column<int>(type: "integer", nullable: false),
                    goal_type = table.Column<short>(type: "smallint", nullable: false),
                    goal_window_days = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_automation_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    first_seen_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rfm_r = table.Column<int>(type: "integer", nullable: true),
                    rfm_f = table.Column<int>(type: "integer", nullable: true),
                    rfm_m = table.Column<int>(type: "integer", nullable: true),
                    rfm_segment = table.Column<short>(type: "smallint", nullable: true),
                    rfm_computed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ai_ltv_12m_profit = table.Column<decimal>(type: "numeric(19,4)", nullable: true),
                    ai_churn_risk = table.Column<int>(type: "integer", nullable: true),
                    ai_next_purchase_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ai_discount_sensitivity = table.Column<int>(type: "integer", nullable: true),
                    ai_computed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message_id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    processed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ingestion_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_type = table.Column<short>(type: "smallint", nullable: false),
                    event_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    provider_event_id = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    signature_valid = table.Column<bool>(type: "boolean", nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    processing_status = table.Column<short>(type: "smallint", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ingestion_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "integration_cursors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cursor_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cursor_value = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integration_cursors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "message_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    channel = table.Column<short>(type: "smallint", nullable: false),
                    language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_message_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<short>(type: "smallint", nullable: false),
                    provider_order_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    placed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    total_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    net_profit = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    net_profit_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    cost_breakdown_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    message_type = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    headers_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "provider_connections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_type = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    external_account_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    access_token_ciphertext = table.Column<string>(type: "text", nullable: false),
                    access_token_key_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    access_token_algorithm = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    refresh_token_ciphertext = table.Column<string>(type: "text", nullable: true),
                    refresh_token_key_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    refresh_token_algorithm = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    access_token_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provider_connections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    roles_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "automation_rule_actions",
                columns: table => new
                {
                    automation_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<short>(type: "smallint", nullable: false),
                    channel = table.Column<short>(type: "smallint", nullable: true),
                    template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    personalization_json = table.Column<string>(type: "jsonb", nullable: true),
                    tag = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    segment_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    task_assignee = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    task_description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_automation_rule_actions", x => new { x.automation_rule_id, x.id });
                    table.ForeignKey(
                        name: "fk_automation_rule_actions_automation_rules_automation_rule_id",
                        column: x => x.automation_rule_id,
                        principalTable: "automation_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "automation_rule_conditions",
                columns: table => new
                {
                    automation_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    field = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    op = table.Column<short>(type: "smallint", nullable: false),
                    value_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_automation_rule_conditions", x => new { x.automation_rule_id, x.id });
                    table.ForeignKey(
                        name: "fk_automation_rule_conditions_automation_rules_automation_rule",
                        column: x => x.automation_rule_id,
                        principalTable: "automation_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_consents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    policy_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    changed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_consents", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_consents_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_identities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<short>(type: "smallint", nullable: false),
                    value_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    value_ciphertext = table.Column<string>(type: "text", nullable: true),
                    value_key_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    value_algorithm = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    source_provider = table.Column<short>(type: "smallint", nullable: true),
                    source_external_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    first_seen_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_identities", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_identities_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_lines",
                columns: table => new
                {
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sku = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    product_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    unit_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    line_total_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_lines", x => new { x.order_id, x.line_id });
                    table.ForeignKey(
                        name: "fk_order_lines_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_automation_rule_actions_automation_rule_id",
                table: "automation_rule_actions",
                column: "automation_rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_automation_rule_conditions_automation_rule_id",
                table: "automation_rule_conditions",
                column: "automation_rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_automation_rules_tenant_id_status",
                table: "automation_rules",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_consents_customer_id_status",
                table: "customer_consents",
                columns: new[] { "customer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_consents_customer_id_type",
                table: "customer_consents",
                columns: new[] { "customer_id", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customer_identities_customer_id",
                table: "customer_identities",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_identities_tenant_id_type_value_hash",
                table: "customer_identities",
                columns: new[] { "tenant_id", "type", "value_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customers_tenant_id_last_seen_at_utc",
                table: "customers",
                columns: new[] { "tenant_id", "last_seen_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_tenant_id_consumer_name_message_id",
                table: "inbox_messages",
                columns: new[] { "tenant_id", "consumer_name", "message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ingestion_events_processing_status_next_attempt_at_utc",
                table: "ingestion_events",
                columns: new[] { "processing_status", "next_attempt_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_ingestion_events_tenant_id_provider_type_provider_event_id",
                table: "ingestion_events",
                columns: new[] { "tenant_id", "provider_type", "provider_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_integration_cursors_tenant_id_provider_connection_id_cursor",
                table: "integration_cursors",
                columns: new[] { "tenant_id", "provider_connection_id", "cursor_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_message_templates_tenant_id_name",
                table: "message_templates",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_order_lines_order_id",
                table: "order_lines",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_channel_provider_order_id",
                table: "orders",
                columns: new[] { "tenant_id", "channel", "provider_order_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_customer_id_placed_at_utc",
                table: "orders",
                columns: new[] { "tenant_id", "customer_id", "placed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status_next_attempt_at_utc",
                table: "outbox_messages",
                columns: new[] { "status", "next_attempt_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_tenant_id_occurred_at_utc",
                table: "outbox_messages",
                columns: new[] { "tenant_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_provider_connections_tenant_id_provider_type",
                table: "provider_connections",
                columns: new[] { "tenant_id", "provider_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_slug",
                table: "tenants",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_tenant_id",
                table: "users",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "automation_rule_actions");

            migrationBuilder.DropTable(
                name: "automation_rule_conditions");

            migrationBuilder.DropTable(
                name: "customer_consents");

            migrationBuilder.DropTable(
                name: "customer_identities");

            migrationBuilder.DropTable(
                name: "inbox_messages");

            migrationBuilder.DropTable(
                name: "ingestion_events");

            migrationBuilder.DropTable(
                name: "integration_cursors");

            migrationBuilder.DropTable(
                name: "message_templates");

            migrationBuilder.DropTable(
                name: "order_lines");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "provider_connections");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "automation_rules");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "orders");
        }
    }
}
