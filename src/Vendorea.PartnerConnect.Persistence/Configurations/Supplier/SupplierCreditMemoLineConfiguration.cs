using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Persistence.Configurations.Supplier;

public class SupplierCreditMemoLineConfiguration : IEntityTypeConfiguration<SupplierCreditMemoLine>
{
    public void Configure(EntityTypeBuilder<SupplierCreditMemoLine> builder)
    {
        builder.ToTable("SupplierCreditMemoLines");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SupplierSku).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CustomerSku).HasMaxLength(100);
        builder.Property(x => x.Upc).HasMaxLength(20);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.UnitOfMeasure).HasMaxLength(10).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.ExtendedCredit).HasPrecision(18, 2);
        builder.Property(x => x.TaxCredit).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasIndex(x => x.SupplierCreditMemoId);
        builder.HasIndex(x => new { x.SupplierCreditMemoId, x.LineNumber }).IsUnique();
        builder.HasIndex(x => x.SupplierSku);

        builder.HasOne(x => x.CreditMemo)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.SupplierCreditMemoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.OriginalInvoiceLine)
            .WithMany()
            .HasForeignKey(x => x.SupplierInvoiceLineId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
