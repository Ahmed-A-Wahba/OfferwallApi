using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Infrastructure.Persistence.Configurations;

public sealed class ClickConfiguration : IEntityTypeConfiguration<Click>
{
    public void Configure(EntityTypeBuilder<Click> builder)
    {
        builder.HasKey(x => x.ClickId);

        builder.Property(x => x.OfferId)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany(x => x.Clicks)
            .HasForeignKey(x => x.UserId);
    }
}