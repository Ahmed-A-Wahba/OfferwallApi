using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfferwallApi.Entities;

namespace OfferwallApi.Infrastructure.Persistence.Configurations;


public sealed class ConversionConfiguration : IEntityTypeConfiguration<Conversion>
{
    public void Configure(EntityTypeBuilder<Conversion> builder)
    {
        builder.HasKey(x => x.ConversionId);

        builder.Property(x => x.OfferId)
            .HasMaxLength(100);

        builder.Property(x => x.EventId)
            .HasMaxLength(100);

        builder.Property(x => x.Payout)
            .HasPrecision(18, 2);

        builder.HasMany(x => x.WalletTransactions)
            .WithOne(x => x.Conversion)
            .HasForeignKey(x => x.ConversionId);
    }
}