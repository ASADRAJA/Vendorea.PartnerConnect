using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Configurations;

public class SprCategoryConfiguration : IEntityTypeConfiguration<SprCategory>
{
    public void Configure(EntityTypeBuilder<SprCategory> builder)
    {
        builder.ToTable("SprCategories");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.CategoryCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.CategoryName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.FullPath)
            .HasMaxLength(500);

        builder.Property(e => e.UnspscCode)
            .HasMaxLength(20);

        // Self-referencing relationship for hierarchy
        builder.HasOne(e => e.ParentCategory)
            .WithMany(e => e.ChildCategories)
            .HasForeignKey(e => e.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(e => e.CategoryCode)
            .IsUnique()
            .HasDatabaseName("IX_SprCategories_Code");

        builder.HasIndex(e => e.ParentCategoryId)
            .HasDatabaseName("IX_SprCategories_Parent");

        builder.HasIndex(e => e.Level)
            .HasDatabaseName("IX_SprCategories_Level");

        builder.HasIndex(e => e.IsActive)
            .HasDatabaseName("IX_SprCategories_Active");
    }
}
