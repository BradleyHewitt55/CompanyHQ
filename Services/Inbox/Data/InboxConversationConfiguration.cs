using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CompanyHQ.Services.Inbox.Contracts.Models;

namespace CompanyHQ.Services.Inbox.Data;

internal sealed class InboxConversationConfiguration : IEntityTypeConfiguration<InboxConversation>
{
    public void Configure(EntityTypeBuilder<InboxConversation> builder)
    {
        builder.Property(conversation => conversation.Name).HasMaxLength(120).IsRequired();
        builder.Property(conversation => conversation.Email).HasMaxLength(254).IsRequired();
        builder.Property(conversation => conversation.Body).HasMaxLength(4000).IsRequired();
        builder.Property(conversation => conversation.Source).HasMaxLength(40).IsRequired();
        builder.Property(conversation => conversation.Origin).HasMaxLength(600);
        builder.Property(conversation => conversation.VisitorTokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(conversation => conversation.LastActivityAt);
        builder.HasIndex(conversation => conversation.ResolvedAt);
    }
}

internal sealed class InboxReplyConfiguration : IEntityTypeConfiguration<InboxReply>
{
    public void Configure(EntityTypeBuilder<InboxReply> builder)
    {
        builder.Property(reply => reply.Body).HasMaxLength(4000).IsRequired();
        builder.HasOne<InboxConversation>()
            .WithMany()
            .HasForeignKey(reply => reply.InboxConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(reply => new { reply.InboxConversationId, reply.CreatedAt });
    }
}
