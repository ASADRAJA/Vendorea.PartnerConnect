using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class OrgAccessRequestConfiguration : IEntityTypeConfiguration<OrgAccessRequest>
{
    public void Configure(EntityTypeBuilder<OrgAccessRequest> builder)
    {
        builder.ToTable("OrgAccessRequests");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.SubmittedOrganizationIdentifier)
            .HasMaxLength(200);

        builder.Property(r => r.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(r => r.DisplayName)
            .HasMaxLength(200);

        builder.Property(r => r.Message)
            .HasMaxLength(1000);

        // Store the status enum as its string name for readability/stability.
        builder.Property(r => r.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(OrgAccessRequestStatus.Pending);

        builder.Property(r => r.DecisionReason)
            .HasMaxLength(1000);

        builder.HasIndex(r => new { r.OrganizationId, r.Status });

        builder.HasOne(r => r.Organization)
            .WithMany()
            .HasForeignKey(r => r.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
