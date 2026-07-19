using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfferwallApi.Entities;

namespace OfferwallApi.Infrastructure.Persistence.Configurations;

public sealed class OfferCacheConfiguration : IEntityTypeConfiguration<OfferCache>
{
    public void Configure(EntityTypeBuilder<OfferCache> builder)
    {
        builder.HasKey(x => x.OfferId);

        builder.Property(x => x.OfferId)
            .HasMaxLength(100);

        builder.Property(x => x.Name)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.Logo)
            .HasMaxLength(500);

        builder.Property(x => x.TotalReward)
            .HasPrecision(18, 2);
    }
}