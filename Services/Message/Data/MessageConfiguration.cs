using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CompanyHQ.Services.Messages.Contracts.Models;
using CompanyHQ.Services.Users.Contracts.Models;

namespace CompanyHQ.Services.Messages.Data;

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.Property(message => message.Body).HasMaxLength(4000).IsRequired();

        builder.HasIndex(message => new { message.ConversationId, message.CreatedAt, message.Id })
            .IsDescending(false, true, false);
        builder.HasIndex(message => new { message.SenderUserId, message.ClientMessageId }).IsUnique();
        builder.HasIndex(message => message.MediaObjectId);

        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(message => message.SenderUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
