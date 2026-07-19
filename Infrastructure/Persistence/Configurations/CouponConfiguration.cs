using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfferwallApi.Entities;

namespace OfferwallApi.Infrastructure.Persistence.Configurations;

public sealed class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.HasKey(x => x.CouponId);

        builder.Property(x => x.Code)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.Property(x => x.Value)
            .HasPrecision(18, 2);

        builder.Property(x => x.CountryCode)
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(x => x.CityCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany(x => x.Coupons)
            .HasForeignKey(x => x.UserId);

        builder.HasOne(x => x.Partner)
            .WithMany(x => x.Coupons)
            .HasForeignKey(x => x.PartnerId);
    }
}