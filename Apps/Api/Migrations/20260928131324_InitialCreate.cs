using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "access_role",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    permissions = table.Column<string[]>(type: "text[]", nullable: false, defaultValue: new string[0]),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_access_role", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "blog_post",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    description = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    body = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    author = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cover_image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_draft = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blog_post", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "company_profile",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    website = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    support_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    info_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    logo_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_profile", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "conversation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_key = table.Column<string>(type: "character varying(73)", maxLength: 73, nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_template",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    html = table.Column<string>(type: "text", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_template", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "faq_entry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    question = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    answer = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    seo_description = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_draft = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_faq_entry", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mcp_o_auth_client",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    redirect_uris = table.Column<string[]>(type: "text[]", nullable: false),
                    grant_types = table.Column<string[]>(type: "text[]", nullable: false),
                    client_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mcp_o_auth_client", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "queued_email",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    to = table.Column<string[]>(type: "text[]", nullable: false),
                    subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    html = table.Column<string>(type: "text", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_priority = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    provider_message_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_queued_email", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    access_role_id = table.Column<Guid>(type: "uuid", nullable: true),
                    first_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    last_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    bio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    profile_image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    email_verified = table.Column<bool>(type: "boolean", nullable: false),
                    mcp_disabled_tools = table.Column<string[]>(type: "text[]", nullable: false, defaultValue: new string[0]),
                    sessions_revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_access_role_access_role_id",
                        column: x => x.access_role_id,
                        principalTable: "access_role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "account_flag",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Active"),
                    raised_by = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    raised_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    evidence = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    cleared_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cleared_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_flag", x => x.id);
                    table.ForeignKey(
                        name: "fk_account_flag_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agent_key",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    prefix = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    is_o_auth = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    o_auth_client_id = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    o_auth_resource = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    access = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    access_role_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mcp_disabled_tools = table.Column<string[]>(type: "text[]", nullable: false, defaultValue: new string[0]),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_key", x => x.id);
                    table.ForeignKey(
                        name: "fk_agent_key_access_role_access_role_id",
                        column: x => x.access_role_id,
                        principalTable: "access_role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agent_key_user_revoked_by_user_id",
                        column: x => x.revoked_by_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agent_key_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "appointment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointment", x => x.id);
                    table.CheckConstraint("ck_appointment_scheduled_at", "scheduled_at > TIMESTAMPTZ '0001-01-02 00:00:00+00'");
                    table.ForeignKey(
                        name: "fk_appointment_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "auth_token",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    pending_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_auth_token_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_report",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Pending"),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_report", x => x.id);
                    table.ForeignKey(
                        name: "fk_content_report_user_reporter_user_id",
                        column: x => x.reporter_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "conversation_participant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_message_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cleared_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    muted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation_participant", x => x.id);
                    table.ForeignKey(
                        name: "fk_conversation_participant_conversation_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_conversation_participant_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hidden_content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Legacy"),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hidden_content", x => x.id);
                    table.ForeignKey(
                        name: "fk_hidden_content_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mcp_authorization_code",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    client_id = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    redirect_uri = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    resource = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    code_challenge = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mcp_authorization_code", x => x.id);
                    table.ForeignKey(
                        name: "fk_mcp_authorization_code_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mcp_o_auth_refresh_token",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    client_id = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    resource = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mcp_o_auth_refresh_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_mcp_o_auth_refresh_token_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "media_object",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    cleanup_failure_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<string>(type: "text", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_object", x => x.id);
                    table.ForeignKey(
                        name: "fk_media_object_user_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "message",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    media_object_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    suppressed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_message", x => x.id);
                    table.ForeignKey(
                        name: "fk_message_conversation_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_message_user_sender_user_id",
                        column: x => x.sender_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_preference",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    push_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_preference", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_preference_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "push_device",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    disabled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_push_device", x => x.id);
                    table.ForeignKey(
                        name: "fk_push_device_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "queued_notification",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    data_json = table.Column<string>(type: "jsonb", nullable: true),
                    channels = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_queued_notification", x => x.id);
                    table.ForeignKey(
                        name: "fk_queued_notification_user_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_token",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    device_label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_token_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_block",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocker_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocked_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_block", x => x.id);
                    table.CheckConstraint("ck_user_block_distinct_users", "blocker_user_id <> blocked_user_id");
                    table.ForeignKey(
                        name: "fk_user_block_user_blocked_user_id",
                        column: x => x.blocked_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_block_user_blocker_user_id",
                        column: x => x.blocker_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_favorite_place",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_favorite_place", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_favorite_place_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_report_decision",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    administrator_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    new_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_report_decision", x => x.id);
                    table.ForeignKey(
                        name: "fk_content_report_decision_content_report_content_report_id",
                        column: x => x.content_report_id,
                        principalTable: "content_report",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_content_report_decision_user_administrator_user_id",
                        column: x => x.administrator_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reported_content_snapshot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    body = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_target = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reported_content_snapshot", x => x.id);
                    table.ForeignKey(
                        name: "fk_reported_content_snapshot_content_report_content_report_id",
                        column: x => x.content_report_id,
                        principalTable: "content_report",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_access_role_name",
                table: "access_role",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_account_flag_status_created_at",
                table: "account_flag",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_account_flag_user_id_kind",
                table: "account_flag",
                columns: new[] { "user_id", "kind" },
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_agent_key_access_role_id",
                table: "agent_key",
                column: "access_role_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_key_key_hash",
                table: "agent_key",
                column: "key_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_key_revoked_by_user_id",
                table: "agent_key",
                column: "revoked_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_key_user_id_revoked_at",
                table: "agent_key",
                columns: new[] { "user_id", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_source_kind_source_id",
                table: "appointment",
                columns: new[] { "source_kind", "source_id" },
                unique: true,
                filter: "source_kind IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_user_id_scheduled_at",
                table: "appointment",
                columns: new[] { "user_id", "scheduled_at" });

            migrationBuilder.CreateIndex(
                name: "ix_auth_token_token_hash",
                table: "auth_token",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auth_token_user_id_purpose",
                table: "auth_token",
                columns: new[] { "user_id", "purpose" });

            migrationBuilder.CreateIndex(
                name: "ix_blog_post_is_draft_published_at_sort_order",
                table: "blog_post",
                columns: new[] { "is_draft", "published_at", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_blog_post_slug",
                table: "blog_post",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_content_report_reporter_user_id_target_kind_target_id",
                table: "content_report",
                columns: new[] { "reporter_user_id", "target_kind", "target_id" },
                unique: true,
                filter: "status <> 'Withdrawn'");

            migrationBuilder.CreateIndex(
                name: "ix_content_report_status_created_at",
                table: "content_report",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_content_report_target_kind_target_id",
                table: "content_report",
                columns: new[] { "target_kind", "target_id" });

            migrationBuilder.CreateIndex(
                name: "ix_content_report_decision_administrator_user_id",
                table: "content_report_decision",
                column: "administrator_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_report_decision_content_report_id_decided_at",
                table: "content_report_decision",
                columns: new[] { "content_report_id", "decided_at" });

            migrationBuilder.CreateIndex(
                name: "ix_conversation_participant_key",
                table: "conversation",
                column: "participant_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conversation_participant_conversation_id_user_id",
                table: "conversation_participant",
                columns: new[] { "conversation_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conversation_participant_user_id_last_message_at",
                table: "conversation_participant",
                columns: new[] { "user_id", "last_message_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_email_template_key",
                table: "email_template",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_faq_entry_is_draft_sort_order_published_at",
                table: "faq_entry",
                columns: new[] { "is_draft", "sort_order", "published_at" });

            migrationBuilder.CreateIndex(
                name: "ix_faq_entry_key",
                table: "faq_entry",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hidden_content_user_id_target_kind",
                table: "hidden_content",
                columns: new[] { "user_id", "target_kind" });

            migrationBuilder.CreateIndex(
                name: "ix_hidden_content_user_id_target_kind_target_id_source_source_",
                table: "hidden_content",
                columns: new[] { "user_id", "target_kind", "target_id", "source", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mcp_authorization_code_code_hash",
                table: "mcp_authorization_code",
                column: "code_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mcp_authorization_code_user_id_expires_at",
                table: "mcp_authorization_code",
                columns: new[] { "user_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_mcp_o_auth_client_client_id",
                table: "mcp_o_auth_client",
                column: "client_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mcp_o_auth_refresh_token_token_hash",
                table: "mcp_o_auth_refresh_token",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mcp_o_auth_refresh_token_user_id_revoked_at",
                table: "mcp_o_auth_refresh_token",
                columns: new[] { "user_id", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "ix_media_object_key",
                table: "media_object",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_object_owner_user_id_created_at",
                table: "media_object",
                columns: new[] { "owner_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_media_object_status_created_at",
                table: "media_object",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_message_conversation_id_created_at_id",
                table: "message",
                columns: new[] { "conversation_id", "created_at", "id" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "ix_message_media_object_id",
                table: "message",
                column: "media_object_id");

            migrationBuilder.CreateIndex(
                name: "ix_message_sender_user_id_client_message_id",
                table: "message",
                columns: new[] { "sender_user_id", "client_message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_preference_user_id",
                table: "notification_preference",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_push_device_token",
                table: "push_device",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_push_device_user_id",
                table: "push_device",
                column: "user_id",
                filter: "disabled_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_queued_email_idempotency_key",
                table: "queued_email",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_queued_email_status_next_attempt_at",
                table: "queued_email",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_queued_notification_recipient_user_id",
                table: "queued_notification",
                column: "recipient_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_queued_notification_status_next_attempt_at",
                table: "queued_notification",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_token_hash",
                table: "refresh_token",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_user_id",
                table: "refresh_token",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_reported_content_snapshot_content_report_id_occurred_at",
                table: "reported_content_snapshot",
                columns: new[] { "content_report_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_user_access_role_id",
                table: "user",
                column: "access_role_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_email",
                table: "user",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_first_name_last_name",
                table: "user",
                columns: new[] { "first_name", "last_name" });

            migrationBuilder.CreateIndex(
                name: "ix_user_block_blocked_user_id",
                table: "user_block",
                column: "blocked_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_block_blocker_user_id_blocked_user_id",
                table: "user_block",
                columns: new[] { "blocker_user_id", "blocked_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_favorite_place_user_id_kind_latitude_longitude_name",
                table: "user_favorite_place",
                columns: new[] { "user_id", "kind", "latitude", "longitude", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_flag");

            migrationBuilder.DropTable(
                name: "agent_key");

            migrationBuilder.DropTable(
                name: "appointment");

            migrationBuilder.DropTable(
                name: "auth_token");

            migrationBuilder.DropTable(
                name: "blog_post");

            migrationBuilder.DropTable(
                name: "company_profile");

            migrationBuilder.DropTable(
                name: "content_report_decision");

            migrationBuilder.DropTable(
                name: "conversation_participant");

            migrationBuilder.DropTable(
                name: "email_template");

            migrationBuilder.DropTable(
                name: "faq_entry");

            migrationBuilder.DropTable(
                name: "hidden_content");

            migrationBuilder.DropTable(
                name: "mcp_authorization_code");

            migrationBuilder.DropTable(
                name: "mcp_o_auth_client");

            migrationBuilder.DropTable(
                name: "mcp_o_auth_refresh_token");

            migrationBuilder.DropTable(
                name: "media_object");

            migrationBuilder.DropTable(
                name: "message");

            migrationBuilder.DropTable(
                name: "notification_preference");

            migrationBuilder.DropTable(
                name: "push_device");

            migrationBuilder.DropTable(
                name: "queued_email");

            migrationBuilder.DropTable(
                name: "queued_notification");

            migrationBuilder.DropTable(
                name: "refresh_token");

            migrationBuilder.DropTable(
                name: "reported_content_snapshot");

            migrationBuilder.DropTable(
                name: "user_block");

            migrationBuilder.DropTable(
                name: "user_favorite_place");

            migrationBuilder.DropTable(
                name: "conversation");

            migrationBuilder.DropTable(
                name: "content_report");

            migrationBuilder.DropTable(
                name: "user");

            migrationBuilder.DropTable(
                name: "access_role");
        }
    }
}
