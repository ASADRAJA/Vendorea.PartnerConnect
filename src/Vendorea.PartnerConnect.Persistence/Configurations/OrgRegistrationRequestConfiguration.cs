using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class OrgRegistrationRequestConfiguration : IEntityTypeConfiguration<OrgRegistrationRequest>
{
    public void Configure(EntityTypeBuilder<OrgRegistrationRequest> builder)
    {
        builder.ToTable("OrgRegistrationRequests");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.OrganizationName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.PlanCode)
            .HasMaxLength(50);

        builder.Property(r => r.AdminDisplayName)
            .HasMaxLength(200);

        builder.Property(r => r.AdminEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(r => r.ContactPhone)
            .HasMaxLength(50);

        // Store the status enum as its string name for readability/stability.
        builder.Property(r => r.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(OrgRegistrationStatus.Pending);

        builder.Property(r => r.DecisionByAdmin)
            .HasMaxLength(200);

        builder.Property(r => r.DecisionReason)
            .HasMaxLength(1000);

        builder.HasIndex(r => r.Status);

        builder.HasOne(r => r.Organization)
            .WithMany()
            .HasForeignKey(r => r.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
