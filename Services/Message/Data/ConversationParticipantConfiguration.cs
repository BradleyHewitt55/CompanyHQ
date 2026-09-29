using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CompanyHQ.Services.Messages.Contracts.Models;
using CompanyHQ.Services.Users.Contracts.Models;

namespace CompanyHQ.Services.Messages.Data;

internal sealed class ConversationParticipantConfiguration : IEntityTypeConfiguration<ConversationParticipant>
{
    public void Configure(EntityTypeBuilder<ConversationParticipant> builder)
    {
        builder.HasIndex(participant => new { participant.ConversationId, participant.UserId }).IsUnique();
        builder.HasIndex(participant => new { participant.UserId, participant.LastMessageAt })
            .IsDescending(false, true);

        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(participant => participant.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(participant => participant.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
