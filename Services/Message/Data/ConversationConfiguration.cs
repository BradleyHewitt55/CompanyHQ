using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CompanyHQ.Services.Messages.Contracts.Models;

namespace CompanyHQ.Services.Messages.Data;

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.Property(conversation => conversation.ParticipantKey).HasMaxLength(73).IsRequired();
        builder.HasIndex(conversation => conversation.ParticipantKey).IsUnique();
    }
}
