using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class AdminPortalUserConfiguration : IEntityTypeConfiguration<AdminPortalUser>
{
    public void Configure(EntityTypeBuilder<AdminPortalUser> builder)
    {
        builder.ToTable("AdminPortalUsers");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(u => u.Username)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(400);

        builder.Property(u => u.DisplayName)
            .HasMaxLength(200);

        // Store the enum as its string name for readability/stability.
        builder.Property(u => u.Role)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true);
    }
}
