using Microsoft.EntityFrameworkCore;
using OfferwallApi.Entities;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Partner> Partners { get; set; }
    public DbSet<PartnerVerificationCode> PartnerVerificationCodes { get; set; }
    public DbSet<PartnerRefreshToken> PartnerRefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}