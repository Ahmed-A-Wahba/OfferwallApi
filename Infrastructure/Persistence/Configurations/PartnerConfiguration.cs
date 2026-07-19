using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Infrastructure.Persistence.Configurations;

public sealed class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    public void Configure(EntityTypeBuilder<Partner> builder)
    {
        builder.HasKey(x => x.PartnerId);

        builder.Property(x => x.FullName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.CompanyName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.WebsiteUrl)
            .HasMaxLength(500);

        builder.Property(x => x.StoreCategory)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.MonthlyActiveUsers)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(x => x.Email)
            .IsUnique();

        builder.Property(x => x.PasswordHash)
            .IsRequired();

        builder.Property(x => x.ApiKey)
            .HasMaxLength(100);

        builder.HasIndex(x => x.ApiKey)
            .IsUnique();

        builder.Property(x => x.AllowedIframeOrigin)
            .HasMaxLength(255);

        builder.HasIndex(x => x.AllowedIframeOrigin)
            .IsUnique();

        builder.Property(x => x.PointsPerUsd)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.AccountHolderName)
            .HasMaxLength(200);

        builder.Property(x => x.BankName)
            .HasMaxLength(150);

        builder.Property(x => x.Iban)
            .HasMaxLength(50);

        builder.Property(x => x.SwiftCode)
            .HasMaxLength(20);

        builder.Property(x => x.CountryCode)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.Property(x => x.IsEmailVerified)
            .HasDefaultValue(false);

        builder.HasMany(x => x.Users)
            .WithOne(x => x.Partner)
            .HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Payouts)
            .WithOne(x => x.Partner)
            .HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Coupons)
            .WithOne(x => x.Partner)
            .HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.RefreshTokens)
            .WithOne(x => x.Partner)
            .HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}