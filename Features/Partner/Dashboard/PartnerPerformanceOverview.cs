using FluentValidation;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using Microsoft.EntityFrameworkCore;
using Carter;
using MediatR;
using OfferwallApi.Infrastructure.Authorization;

namespace OfferwallApi.Features.Partner.Dashboard;

public static class PartnerPerformanceOverview
{
    public sealed record Query(
        DateOnly From,
        DateOnly To
    ) : IQuery<Result<Response>>;

    public sealed record Response(
        IReadOnlyList<ChartPoint> Generated,
        IReadOnlyList<ChartPoint> Redeemed
    );

    public sealed record ChartPoint(
        DateOnly Date,
        int Count
    );

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.From)
                .LessThanOrEqualTo(x => x.To)
                .WithMessage("'From' date must be less than or equal to 'To' date.");

            RuleFor(x => x.To)
                .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("'To' date cannot be in the future.");

            RuleFor(x => x)
                .Must(x => x.To.DayNumber - x.From.DayNumber <= 365)
                .WithMessage("The selected date range cannot exceed 365 days.");
        }
    }

    [Authorize(Role = "Partner")]
    internal sealed class Handler(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser)
    : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            var partnerId = currentUser.UserId;

            var from = request.From.ToDateTime(TimeOnly.MinValue);
            var to = request.To.ToDateTime(TimeOnly.MaxValue);

            var generated = await dbContext.Coupons
                .Where(x =>
                    x.PartnerId == partnerId &&
                    x.CreatedAt >= from &&
                    x.CreatedAt <= to)
                .GroupBy(x => DateOnly.FromDateTime(x.CreatedAt))
                .Select(g => new ChartPoint(
                    g.Key,
                    g.Count()))
                .OrderBy(x => x.Date)
                .ToListAsync(cancellationToken);

            var redeemed = await dbContext.Coupons
                .Where(x =>
                    x.PartnerId == partnerId &&
                    x.RedeemedAt != null &&
                    x.RedeemedAt >= from &&
                    x.RedeemedAt <= to)
                .GroupBy(x => DateOnly.FromDateTime(x.RedeemedAt!.Value))
                .Select(g => new ChartPoint(
                    g.Key,
                    g.Count()))
                .OrderBy(x => x.Date)
                .ToListAsync(cancellationToken);

            return new Response(
                generated,
                redeemed);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet(
                "/partners/dashboard/performance-overview",
                async (
                    [AsParameters] Query query,
                    ISender sender) =>
                {
                    var result = await sender.Send(query);

                    return result.ToHttpResponse();
                })
                .RequireAuthorization();
        }
    }
}