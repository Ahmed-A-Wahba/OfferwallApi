using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Infrastructure.BackgroundJobs;

public class PayoutGenerationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PayoutGenerationBackgroundService> _logger;

    public PayoutGenerationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<PayoutGenerationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Payout Generation Background Job started.");

        // يعمل كل 24 ساعة
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessMonthlyPayoutsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while generating monthly payouts.");
            }
        }
    }

    private async Task ProcessMonthlyPayoutsAsync(CancellationToken cancellationToken)
    {
        // بما أن BackgroundService هو Singleton، نحتاج لفتح Scope جديد لاستدعاء DbContext (الذي يكون Scoped)
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var today = DateTime.UtcNow.Date;

        // نظام 30 يوم (Net 30) يتم فيه حساب أرباح الشهر السابق في اليوم الأول من الشهر الجديد
        // يمكنك تغيير "1" إذا كنت تريد تشغيلها في يوم محدد من الشهر
        if (today.Day != 1)
        {
            return;
        }

        // تحديد بداية ونهاية الشهر السابق
        var periodStart = today.AddMonths(-1);
        var periodEnd = today.AddTicks(-1); // أخر لحظة في الشهر السابق

        _logger.LogInformation("Checking payouts for period: {PeriodStart} to {PeriodEnd}", periodStart, periodEnd);

        // جلب كل الشركاء النشطين
        var partners = await dbContext.Partners
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var partner in partners)
        {
            // التحقق لتجنب تكرار إنشاء Payout لنفس الشريك ونفس الفترة
            var payoutExists = await dbContext.Payouts.AnyAsync(p =>
                p.PartnerId == partner.PartnerId &&
                p.PeriodStart == periodStart,
                cancellationToken);

            if (payoutExists)
                continue;

            // حساب عدد التحويلات (Conversions) وقيمتها المالية للشهر السابق
            // ملاحظة: قم بتعديل المعادلة المالية أدناه (c.Revenue) بناءً على الحقل الفعلي في الـ Entity الخاص بك
            var monthlyData = await dbContext.Conversions
                .Where(c => c.PartnerId == partner.PartnerId &&
                            c.Type == ConversionType.Conversion && // تأكد من استخدام حالة الاعتماد الخاصة بك
                            c.CreatedAt >= periodStart &&
                            c.CreatedAt <= periodEnd)
                .GroupBy(c => c.PartnerId)
                .Select(g => new
                {
                    RedemptionCount = g.Count(),
                    // افترضنا أن لديك حقل يمثل قيمة العائد للشريك (مثلاً Revenue أو Payout)
                    TotalAmount = g.Sum(c => c.Payout) // قم بتعديل هذا بناءً على الحقل الفعلي الذي يمثل العائد المالي
                })
                .FirstOrDefaultAsync(cancellationToken);

            // إذا لم يكن هناك أي نشاط للشريك في هذا الشهر، يمكننا تخطي إنشاء الفاتورة
            if (monthlyData == null || monthlyData.RedemptionCount == 0)
                continue;

            var payout = new Payout
            {
                PayoutId = Guid.NewGuid(),
                PartnerId = partner.PartnerId,
                Amount = monthlyData.TotalAmount,
                RedemptionCount = monthlyData.RedemptionCount,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                Status = PayoutStatus.Pending, // الفاتورة معلقة حتى يتم دفعها يدوياً بعد 30 يوم
                Notes = $"Auto-generated Net-30 payout for {periodStart:MMMM yyyy}.",
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Payouts.Add(payout);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Successfully processed and saved payouts for {Month}.", periodStart.ToString("MMMM yyyy"));
    }
}