using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGCAE.Domain.Entities;
using SGCAE.Domain.ValueObjects;

namespace SGCAE.Infrastructure.Persistence.Configurations;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees", table =>
            table.HasCheckConstraint("CK_Employees_Status", "[Status] IN (1, 2)"));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Identification)
            .HasConversion(v => v.Value, v => new EmployeeIdentification(v))
            .HasMaxLength(30).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.HasIndex(e => e.Identification).IsUnique();
        builder.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.LastName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Email)
            .HasConversion(v => v.Value, v => new EmailAddress(v))
            .HasMaxLength(254).IsRequired();
        builder.Property(e => e.JobTitle).HasMaxLength(100).IsRequired();
        builder.Property(e => e.OrganizationalArea).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Status).IsRequired();
    }
}
