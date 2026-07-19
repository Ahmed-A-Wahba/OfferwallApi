using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfferwallApi.Entities;

namespace OfferwallApi.Infrastructure.Persistence.Configurations;

public sealed class PartnerRefreshTokenConfiguration : IEntityTypeConfiguration<PartnerRefreshToken>
{
    public void Configure(EntityTypeBuilder<PartnerRefreshToken> builder)
    {
        builder.HasKey(x => x.RefreshTokenId);

        builder.Property(x => x.Token)
            .IsRequired();

        builder.HasIndex(x => x.Token)
            .IsUnique();

        builder.HasOne(x => x.Partner)
            .WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => x.PartnerId);
    }
}