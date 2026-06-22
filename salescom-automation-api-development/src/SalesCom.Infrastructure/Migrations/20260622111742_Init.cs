using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SalesCom.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "salescomdbtst");

            migrationBuilder.CreateTable(
                name: "approval_flows",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    flow_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_flows", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    application_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    action_type = table.Column<int>(type: "integer", nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_columns = table.Column<string>(type: "text", nullable: true),
                    old_values = table.Column<string>(type: "jsonb", nullable: true),
                    new_values = table.Column<string>(type: "jsonb", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "channels",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    channel_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "data_sources",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    source_table_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    table_description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_sources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_notifications",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    to_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    cc = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    bcc = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    body = table.Column<string>(type: "text", nullable: false),
                    from_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_notifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "login_logs",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    full_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    login_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    login_status = table.Column<int>(type: "integer", nullable: false),
                    remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sms_notifications",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    phone_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    messages = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sms_notifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    full_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    mobile_no = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    department = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "approval_flow_levels",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    approval_flow_id = table.Column<long>(type: "bigint", nullable: false),
                    approval_type = table.Column<int>(type: "integer", nullable: false),
                    level_order = table.Column<int>(type: "integer", nullable: false),
                    level_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_flow_levels", x => x.id);
                    table.ForeignKey(
                        name: "FK_approval_flow_levels_approval_flows_approval_flow_id",
                        column: x => x.approval_flow_id,
                        principalSchema: "salescomdbtst",
                        principalTable: "approval_flows",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "report_setups",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    report_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    report_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    channel_type_id = table.Column<long>(type: "bigint", nullable: false),
                    commission_cycle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_setup_complete = table.Column<bool>(type: "boolean", nullable: false),
                    is_recurrent = table.Column<bool>(type: "boolean", nullable: false),
                    recurrent_type = table.Column<int>(type: "integer", maxLength: 100, nullable: false),
                    is_ev_disbursement = table.Column<bool>(type: "boolean", nullable: false),
                    ev_disbursement_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    is_pos_disbursement = table.Column<bool>(type: "boolean", nullable: false),
                    definition = table.Column<string>(type: "jsonb", nullable: true),
                    run_start_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    run_end_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_report_stop = table.Column<bool>(type: "boolean", nullable: false),
                    sms_content = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    approval_flow_id = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_setups", x => x.id);
                    table.ForeignKey(
                        name: "FK_report_setups_approval_flows_approval_flow_id",
                        column: x => x.approval_flow_id,
                        principalSchema: "salescomdbtst",
                        principalTable: "approval_flows",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_report_setups_channels_channel_type_id",
                        column: x => x.channel_type_id,
                        principalSchema: "salescomdbtst",
                        principalTable: "channels",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "user_rights",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    rights_code = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_rights", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_rights_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "salescomdbtst",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "approval_flow_level_users",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    approval_flow_level_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_flow_level_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_approval_flow_level_users_approval_flow_levels_approval_flo~",
                        column: x => x.approval_flow_level_id,
                        principalSchema: "salescomdbtst",
                        principalTable: "approval_flow_levels",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_approval_flow_level_users_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "salescomdbtst",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "report_approvals",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    report_setup_id = table.Column<long>(type: "bigint", nullable: false),
                    approval_flow_id = table.Column<long>(type: "bigint", nullable: false),
                    current_level_order = table.Column<int>(type: "integer", nullable: false),
                    overall_status = table.Column<int>(type: "integer", nullable: false),
                    initiated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    initiated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_approvals", x => x.id);
                    table.ForeignKey(
                        name: "FK_report_approvals_approval_flows_approval_flow_id",
                        column: x => x.approval_flow_id,
                        principalSchema: "salescomdbtst",
                        principalTable: "approval_flows",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_report_approvals_report_setups_report_setup_id",
                        column: x => x.report_setup_id,
                        principalSchema: "salescomdbtst",
                        principalTable: "report_setups",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "report_runs",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    report_setup_id = table.Column<long>(type: "bigint", nullable: false),
                    run_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    run_type = table.Column<int>(type: "integer", nullable: false),
                    triggered_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    run_status = table.Column<int>(type: "integer", maxLength: 50, nullable: false),
                    disburse_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_report_runs_report_setups_report_setup_id",
                        column: x => x.report_setup_id,
                        principalSchema: "salescomdbtst",
                        principalTable: "report_setups",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "report_supporting_uploads",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    report_setup_id = table.Column<long>(type: "bigint", nullable: false),
                    db_table_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    db_schema = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    object_bucket = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    object_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    row_count = table.Column<int>(type: "integer", nullable: true),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    uploaded_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_supporting_uploads", x => x.id);
                    table.ForeignKey(
                        name: "FK_report_supporting_uploads_report_setups_report_setup_id",
                        column: x => x.report_setup_id,
                        principalSchema: "salescomdbtst",
                        principalTable: "report_setups",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "section_wise_report_sqls",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    report_setup_id = table.Column<long>(type: "bigint", nullable: false),
                    stage_order = table.Column<int>(type: "integer", nullable: false),
                    sql_text = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_section_wise_report_sqls", x => x.id);
                    table.ForeignKey(
                        name: "FK_section_wise_report_sqls_report_setups_report_setup_id",
                        column: x => x.report_setup_id,
                        principalSchema: "salescomdbtst",
                        principalTable: "report_setups",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "report_approval_details",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    approval_request_id = table.Column<long>(type: "bigint", nullable: false),
                    level_order = table.Column<int>(type: "integer", nullable: false),
                    approval_status = table.Column<int>(type: "integer", nullable: false),
                    remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    approval_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    approval_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_approval_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_report_approval_details_report_approvals_approval_request_id",
                        column: x => x.approval_request_id,
                        principalSchema: "salescomdbtst",
                        principalTable: "report_approvals",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ev_disburses",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    report_run_id = table.Column<long>(type: "bigint", nullable: false),
                    channel_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ev_msisdn = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    disburse_status = table.Column<int>(type: "integer", maxLength: 50, nullable: false),
                    disburse_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ev_disburses", x => x.id);
                    table.ForeignKey(
                        name: "FK_ev_disburses_report_runs_report_run_id",
                        column: x => x.report_run_id,
                        principalSchema: "salescomdbtst",
                        principalTable: "report_runs",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "final_commissions",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    report_run_id = table.Column<long>(type: "bigint", nullable: false),
                    channel_id = table.Column<long>(type: "bigint", nullable: false),
                    channel_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    msisdn = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    commission_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_final_commissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_final_commissions_channels_channel_id",
                        column: x => x.channel_id,
                        principalSchema: "salescomdbtst",
                        principalTable: "channels",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_final_commissions_report_runs_report_run_id",
                        column: x => x.report_run_id,
                        principalSchema: "salescomdbtst",
                        principalTable: "report_runs",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "pos_disbursements",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    report_run_id = table.Column<long>(type: "bigint", nullable: false),
                    dump_status = table.Column<int>(type: "integer", maxLength: 50, nullable: false),
                    disburse_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pos_disbursements", x => x.id);
                    table.ForeignKey(
                        name: "FK_pos_disbursements_report_runs_report_run_id",
                        column: x => x.report_run_id,
                        principalSchema: "salescomdbtst",
                        principalTable: "report_runs",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "run_stages",
                schema: "salescomdbtst",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    run_id = table.Column<long>(type: "bigint", nullable: false),
                    sql_text = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    run_status = table.Column<int>(type: "integer", maxLength: 50, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    document_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    bucket = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    object_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    file_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    file_generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    output_table_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cleanup_status = table.Column<int>(type: "integer", maxLength: 50, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_stages", x => x.id);
                    table.ForeignKey(
                        name: "FK_run_stages_report_runs_run_id",
                        column: x => x.run_id,
                        principalSchema: "salescomdbtst",
                        principalTable: "report_runs",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_approval_flow_level_users_user_id",
                schema: "salescomdbtst",
                table: "approval_flow_level_users",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_approval_flow_level_users_level_user",
                schema: "salescomdbtst",
                table: "approval_flow_level_users",
                columns: new[] { "approval_flow_level_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_approval_flow_levels_flow_order",
                schema: "salescomdbtst",
                table: "approval_flow_levels",
                columns: new[] { "approval_flow_id", "level_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity",
                schema: "salescomdbtst",
                table: "audit_logs",
                columns: new[] { "entity_name", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ux_channels_channel_name",
                schema: "salescomdbtst",
                table: "channels",
                column: "channel_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_data_sources_source_table",
                schema: "salescomdbtst",
                table: "data_sources",
                column: "source_table_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ev_disburse_report_run",
                schema: "salescomdbtst",
                table: "ev_disburses",
                column: "report_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_final_commissions_channel",
                schema: "salescomdbtst",
                table: "final_commissions",
                column: "channel_id");

            migrationBuilder.CreateIndex(
                name: "ux_final_commissions_run_channel_code",
                schema: "salescomdbtst",
                table: "final_commissions",
                columns: new[] { "report_run_id", "channel_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pos_disbursement_report_run",
                schema: "salescomdbtst",
                table: "pos_disbursements",
                column: "report_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_approval_details_approval",
                schema: "salescomdbtst",
                table: "report_approval_details",
                column: "approval_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_approvals_flow",
                schema: "salescomdbtst",
                table: "report_approvals",
                column: "approval_flow_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_approvals_report_setup",
                schema: "salescomdbtst",
                table: "report_approvals",
                column: "report_setup_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_runs_report_setup",
                schema: "salescomdbtst",
                table: "report_runs",
                column: "report_setup_id");

            migrationBuilder.CreateIndex(
                name: "IX_report_setups_approval_flow_id",
                schema: "salescomdbtst",
                table: "report_setups",
                column: "approval_flow_id");

            migrationBuilder.CreateIndex(
                name: "IX_report_setups_channel_type_id",
                schema: "salescomdbtst",
                table: "report_setups",
                column: "channel_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_supporting_uploads_report_setup",
                schema: "salescomdbtst",
                table: "report_supporting_uploads",
                column: "report_setup_id");

            migrationBuilder.CreateIndex(
                name: "ix_run_stages_run",
                schema: "salescomdbtst",
                table: "run_stages",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "ux_section_wise_report_sqls_setup_order",
                schema: "salescomdbtst",
                table: "section_wise_report_sqls",
                columns: new[] { "report_setup_id", "stage_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_user_rights_user_right",
                schema: "salescomdbtst",
                table: "user_rights",
                columns: new[] { "user_id", "rights_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_users_user_id",
                schema: "salescomdbtst",
                table: "users",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_flow_level_users",
                schema: "salescomdbtst");

            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "salescomdbtst");

            migrationBuilder.DropTable(
                name: "data_sources",
                schema: "salescomdbtst");

            migrationBuilder.DropTable(
                name: "email_notifications",
                schema: "salescomdbtst");

            migrationBuilder.DropTable(
                name: "ev_disburses",
                schema: "salescomdbtst");

            migrationBuilder.DropTable(
                name: "final_commissions",
                schema: "salescomdbtst");

            migrationBuilder.DropTable(
                name: "login_logs",
                schema: "salescomdbtst");

            migrationBuilder.DropTable(
                name: "pos_disbursements",
                schema: "salescomdbtst");

            migrationBuilder.DropTable(
                name: "report_approval_details",
                schema: "salescomdbtst");

            migrationBuilder.DropTable(
                name: "report_supporting_uploads",
                schema: "salescomdbtst");

            migrationBuilder.DropTable(
                name: "run_stages",
                schema: "salescomdbtst");

            migrationBuilder.DropTable(
                name: "section_wise_report_sqls",
                schema: "salescomdbtst");

            migrationBuilder.DropTable(
                name: "sms_notifications",
                schema: "salescomdbtst");

            migrationBuilder.DropTable(
                name: "user_rights",
                schema: "salescomdbtst");

            migrationBuilder.DropTable(
                name: "approval_flow_levels",
                schema: "salescomdbtst");

            migrationBuilder.DropTable(
                name: "report_approvals",
                schema: "salescomdbtst");

            migrationBuilder.DropTable(
                name: "report_runs",
                schema: "salescomdbtst");

            migrationBuilder.DropTable(
                name: "users",
                schema: "salescomdbtst");

            migrationBuilder.DropTable(
                name: "report_setups",
                schema: "salescomdbtst");

            migrationBuilder.DropTable(
                name: "approval_flows",
                schema: "salescomdbtst");

            migrationBuilder.DropTable(
                name: "channels",
                schema: "salescomdbtst");
        }
    }
}
