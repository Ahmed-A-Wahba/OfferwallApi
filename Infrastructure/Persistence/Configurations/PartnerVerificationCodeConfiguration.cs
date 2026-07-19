using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Infrastructure.Persistence.Configurations;

public sealed class PartnerVerificationCodeConfiguration
    : IEntityTypeConfiguration<PartnerVerificationCode>
{
    public void Configure(EntityTypeBuilder<PartnerVerificationCode> builder)
    {
        builder.HasKey(x => x.VerificationCodeId);

        builder.Property(x => x.Code)
            .HasMaxLength(6)
            .IsRequired();

        builder.Property(x => x.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.AttemptCount)
            .HasDefaultValue(0);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.Partner)
            .WithMany(x => x.VerificationCodes)
            .HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.PartnerId);

        builder.HasIndex(x => new
        {
            x.PartnerId,
            x.Type,
            x.Code
        });
    }
}