using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CompanyHQ.Services.Appointments.Contracts.Models;

namespace CompanyHQ.Services.Appointments.Data;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.Property(appointment => appointment.Title).HasMaxLength(160);
        builder.Property(appointment => appointment.Location).HasMaxLength(200);
        builder.Property(appointment => appointment.Notes).HasMaxLength(2_000);
        builder.Property(appointment => appointment.SourceKind).HasMaxLength(64);

        builder.ToTable(table => table.HasCheckConstraint(
            "ck_appointment_scheduled_at",
            "scheduled_at > TIMESTAMPTZ '0001-01-02 00:00:00+00'"));

        // The diary list is always "mine, in time order". This index is the whole query plan.
        builder.HasIndex(appointment => new { appointment.UserId, appointment.ScheduledAt });

        // The idempotency of IAppointmentScheduler is this constraint, not a check-then-insert in
        // the service: two concurrent saves of the same source row cannot both create an appointment.
        builder.HasIndex(appointment => new { appointment.SourceKind, appointment.SourceId })
            .IsUnique()
            .HasFilter("source_kind IS NOT NULL");

        // A diary goes with its account.
        builder.HasOne(appointment => appointment.User)
            .WithMany()
            .HasForeignKey(appointment => appointment.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
