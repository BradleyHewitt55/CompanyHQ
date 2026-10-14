using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CompanyHQ.Messages.Contracts.Models;

namespace CompanyHQ.Messages.Data;

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.Property(conversation => conversation.ParticipantKey).HasMaxLength(73).IsRequired();
        builder.Property(conversation => conversation.ContextKey).HasMaxLength(225).HasDefaultValue(string.Empty).IsRequired();
        builder.Property(conversation => conversation.ContextKind).HasMaxLength(64);
        builder.Property(conversation => conversation.ContextId).HasMaxLength(160);
        builder.HasIndex(conversation => new { conversation.ParticipantKey, conversation.ContextKey }).IsUnique();
    }
}
