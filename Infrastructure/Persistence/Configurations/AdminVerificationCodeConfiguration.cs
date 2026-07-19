using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Infrastructure.Persistence.Configurations;

public sealed class AdminVerificationCodeConfiguration
    : IEntityTypeConfiguration<AdminVerificationCode>
{
    public void Configure(EntityTypeBuilder<AdminVerificationCode> builder)
    {
        builder.HasKey(x => x.VerificationCodeId);

        builder.Property(x => x.Code)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.Type)
            .IsRequired();

        builder.Property(x => x.AttemptCount)
            .HasDefaultValue(0);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .IsRequired();

        builder.HasIndex(x => x.AdminId);

        builder.HasIndex(x => new
        {
            x.AdminId,
            x.Type
        });

        builder.HasOne(x => x.Admin)
            .WithMany(x => x.VerificationCodes)
            .HasForeignKey(x => x.AdminId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}