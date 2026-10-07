using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPublishingAndConversationContexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_conversation_participant_key",
                table: "conversation");

            migrationBuilder.AddColumn<string>(
                name: "context_id",
                table: "conversation",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "context_key",
                table: "conversation",
                type: "character varying(225)",
                maxLength: 225,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "context_kind",
                table: "conversation",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "public_emails",
                table: "company_profile",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cover_media_object_id",
                table: "blog_post",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_conversation_participant_key_context_key",
                table: "conversation",
                columns: new[] { "participant_key", "context_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM conversation GROUP BY participant_key HAVING COUNT(*) > 1) THEN
                    RAISE EXCEPTION 'Cannot remove conversation contexts while a participant pair has multiple conversations.';
                  END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "ix_conversation_participant_key_context_key",
                table: "conversation");

            migrationBuilder.DropColumn(
                name: "context_id",
                table: "conversation");

            migrationBuilder.DropColumn(
                name: "context_key",
                table: "conversation");

            migrationBuilder.DropColumn(
                name: "context_kind",
                table: "conversation");

            migrationBuilder.DropColumn(
                name: "public_emails",
                table: "company_profile");

            migrationBuilder.DropColumn(
                name: "cover_media_object_id",
                table: "blog_post");

            migrationBuilder.CreateIndex(
                name: "ix_conversation_participant_key",
                table: "conversation",
                column: "participant_key",
                unique: true);
        }
    }
}
