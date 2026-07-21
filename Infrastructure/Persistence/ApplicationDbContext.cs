using Microsoft.EntityFrameworkCore;
using OfferwallApi.Entities;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Partner> Partners { get; set; }
    public DbSet<PartnerVerificationCode> PartnerVerificationCodes { get; set; }
    public DbSet<PartnerRefreshToken> PartnerRefreshTokens { get; set; }
    public DbSet<Coupon> Coupons { get; set; }
    public DbSet<Payout> Payouts { get; set; }
    public DbSet<WalletTransaction> WalletTransactions { get; set; }
    public DbSet<Conversion> Conversions { get; set; }
    public DbSet<Click> Clicks { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<SupportTicket> SupportTickets { get; set; }
    public DbSet<OfferCache> OfferCaches { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}