using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGCAE.Domain.Entities;
using SGCAE.Domain.ValueObjects;

namespace SGCAE.Infrastructure.Persistence.Configurations;

internal sealed class AccessRecordConfiguration : IEntityTypeConfiguration<AccessRecord>
{
    public void Configure(EntityTypeBuilder<AccessRecord> builder)
    {
        builder.ToTable("AccessRecords", table =>
        {
            table.HasCheckConstraint("CK_AccessRecords_Movement", "[Movement] IN (1, 2)");
            table.HasCheckConstraint("CK_AccessRecords_Result",
                "([Result] = 1 AND [RejectionReason] IS NULL AND [EmployeeId] IS NOT NULL " +
                "AND [CredentialId] IS NOT NULL AND [AccessPointId] IS NOT NULL) OR " +
                "([Result] = 2 AND [RejectionReason] IS NOT NULL AND [RejectionReason] BETWEEN 1 AND 7)");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.RequestedCredentialCode)
            .HasConversion(v => v.Value, v => new CredentialCode(v))
            .HasMaxLength(100).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.Property(r => r.OccurredAt).HasPrecision(7).IsRequired();
        builder.Property(r => r.RequestedAccessPointId).IsRequired();
        builder.Property(r => r.Movement).IsRequired();
        builder.Property(r => r.Result).IsRequired();
        builder.HasOne<Employee>().WithMany().HasForeignKey(r => r.EmployeeId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Credential>().WithMany().HasForeignKey(r => r.CredentialId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<AccessPoint>().WithMany().HasForeignKey(r => r.AccessPointId)
            .OnDelete(DeleteBehavior.NoAction);

        // El identificador solicitado se conserva incluso si el punto no existe;
        // solo AccessPointId es una referencia opcional a la tabla de puntos.
        builder.HasIndex(r => new { r.OccurredAt, r.Id });
        builder.HasIndex(r => new { r.EmployeeId, r.OccurredAt, r.Id });
        builder.HasIndex(r => new { r.AccessPointId, r.OccurredAt, r.Id });
        builder.HasIndex(r => new { r.Result, r.OccurredAt, r.Id });
    }
}
