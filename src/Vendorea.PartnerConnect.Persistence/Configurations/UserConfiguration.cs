using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.ExternalId)
            .HasMaxLength(200);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.FirstName)
            .HasMaxLength(100);

        builder.Property(u => u.LastName)
            .HasMaxLength(100);

        builder.Property(u => u.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(u => u.Preferences)
            .HasColumnType("nvarchar(max)");

        // Unique constraint on Email
        builder.HasIndex(u => u.Email)
            .IsUnique();

        // Index on ExternalId for identity provider lookups
        builder.HasIndex(u => u.ExternalId)
            .HasFilter("[ExternalId] IS NOT NULL");

        // Index on DealerId for filtering
        builder.HasIndex(u => u.DealerId);

        // Index on Status for filtering
        builder.HasIndex(u => u.Status);

        // Composite index for active users by dealer
        builder.HasIndex(u => new { u.DealerId, u.Status });
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");

        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        builder.Property(ur => ur.AssignedBy)
            .HasMaxLength(200);

        builder.HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index on ExpiresAt for cleanup
        builder.HasIndex(ur => ur.ExpiresAt)
            .HasFilter("[ExpiresAt] IS NOT NULL");
    }
}
