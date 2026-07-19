using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfferwallApi.Entities;

namespace OfferwallApi.Infrastructure.Persistence.Configurations;

public sealed class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.HasKey(x => x.TransactionId);

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2);

        builder.HasOne(x => x.User)
            .WithMany(x => x.WalletTransactions)
            .HasForeignKey(x => x.UserId);

        builder.HasOne(x => x.Conversion)
            .WithMany(x => x.WalletTransactions)
            .HasForeignKey(x => x.ConversionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Coupon)
            .WithMany(x => x.WalletTransactions)
            .HasForeignKey(x => x.CouponId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}