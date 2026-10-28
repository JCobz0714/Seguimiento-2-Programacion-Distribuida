using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGCAE.Domain.Entities;
using SGCAE.Domain.ValueObjects;

namespace SGCAE.Infrastructure.Persistence.Configurations;

internal sealed class CredentialConfiguration : IEntityTypeConfiguration<Credential>
{
    public void Configure(EntityTypeBuilder<Credential> builder)
    {
        builder.ToTable("Credentials", table =>
            table.HasCheckConstraint("CK_Credentials_Status", "[Status] IN (1, 2, 3)"));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Code)
            .HasConversion(v => v.Value, v => new CredentialCode(v))
            .HasMaxLength(100).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.HasIndex(c => c.Code).IsUnique();
        builder.Property(c => c.Status).IsRequired();
        builder.HasOne<Employee>().WithMany().HasForeignKey(c => c.EmployeeId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
