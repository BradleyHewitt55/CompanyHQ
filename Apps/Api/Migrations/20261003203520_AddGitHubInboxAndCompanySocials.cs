using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubInboxAndCompanySocials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "facebook_account",
                table: "company_profile",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "instagram_account",
                table: "company_profile",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "whats_app_number",
                table: "company_profile",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "git_hub_connection",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    repository = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    token_encrypted = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_git_hub_connection", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_conversation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    origin = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    visitor_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_activity_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_conversation", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_reply",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inbox_conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    from_visitor = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_reply", x => x.id);
                    table.ForeignKey(
                        name: "fk_inbox_reply_inbox_conversation_inbox_conversation_id",
                        column: x => x.inbox_conversation_id,
                        principalTable: "inbox_conversation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inbox_conversation_last_activity_at",
                table: "inbox_conversation",
                column: "last_activity_at");

            migrationBuilder.CreateIndex(
                name: "ix_inbox_conversation_resolved_at",
                table: "inbox_conversation",
                column: "resolved_at");

            migrationBuilder.CreateIndex(
                name: "ix_inbox_reply_inbox_conversation_id_created_at",
                table: "inbox_reply",
                columns: new[] { "inbox_conversation_id", "created_at" });
            // The built-in Admin role holds every permission. The seeder keeps it that way on each
            // boot, but a host started with Postgres:AutoSeed off must not lose access to the actions
            // that moved from the Admin role name to a named permission in this release.
            migrationBuilder.Sql(
                """
                UPDATE access_role
                SET permissions = ARRAY[
                    'appointments.manage', 'company.manage', 'conversations.manage', 'create_blog_post',
                    'create_faq_entry', 'delete_blog_post', 'delete_faq_entry', 'get_blog_post', 'get_email_template',
                    'get_faq_entry', 'get_my_notification_preferences', 'github.manage', 'inbox.manage',
                    'list_blog_posts', 'list_email_templates', 'list_faq_entries', 'media.manage', 'moderation.manage',
                    'preview_email_template', 'reset_email_template', 'roles.manage', 'search_area', 'update_blog_post',
                    'update_email_template', 'update_faq_entry', 'users.manage', 'users.read_private']::text[]
                WHERE name = 'Admin';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "git_hub_connection");

            migrationBuilder.DropTable(
                name: "inbox_reply");

            migrationBuilder.DropTable(
                name: "inbox_conversation");

            migrationBuilder.DropColumn(
                name: "facebook_account",
                table: "company_profile");

            migrationBuilder.DropColumn(
                name: "instagram_account",
                table: "company_profile");

            migrationBuilder.DropColumn(
                name: "whats_app_number",
                table: "company_profile");
        }
    }
}
