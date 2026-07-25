using FluentValidation;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using Microsoft.EntityFrameworkCore;
using Carter;
using MediatR;
using OfferwallApi.Infrastructure.Authorization;
using System.Text;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Partner.Dashboard;

public static class PartnerPerformanceExport
{
    [Authorize(Role = "Partner")]
    public sealed record Query(
        DateOnly From,
        DateOnly To
    ) : IQuery<Result<Response>>;

    public sealed record Response(
        IReadOnlyList<ExportRow> Rows
    );

    public sealed record ExportRow(
        DateTime CreatedAt,
        Guid UserId,
        Guid CouponId,
        decimal UsdValue,
        CouponStatus Status
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

            var rows = await dbContext.Coupons
                .Where(x =>
                    x.PartnerId == partnerId &&
                    x.CreatedAt >= from &&
                    x.CreatedAt <= to)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new ExportRow(
                    x.CreatedAt,
                    x.UserId,
                    x.CouponId,
                    x.Value,
                    x.Status
                ))
                .ToListAsync(cancellationToken);

            return new Response(rows);
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet(
                "/partners/dashboard/performance-overview/export",
                async (
                    [AsParameters] Query query,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(query, cancellationToken);

                    if (!result.IsSuccess)
                    {
                        return result.ToHttpResponse();
                    }

                    var csv = BuildCsv(result.Value.Rows);
                    var fileName = $"nexus-performance-{query.From:yyyy-MM-dd}-to-{query.To:yyyy-MM-dd}.csv";
                    var bytes = Encoding.UTF8.GetBytes(csv);

                    return Results.File(bytes, "text/csv", fileName);
                });
        }

        private static string BuildCsv(IReadOnlyList<ExportRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Created Date,User ID,Transaction ID,USD Value,Status");

            foreach (var row in rows)
            {
                sb.AppendLine(string.Join(",",
                    row.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    EscapeCsvField(row.UserId.ToString()),
                    EscapeCsvField(row.CouponId.ToString()),
                    row.UsdValue.ToString("F2"),
                    EscapeCsvField(row.Status.ToString())
                ));
            }

            return sb.ToString();
        }

        private static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }

            return field;
        }
    }
}