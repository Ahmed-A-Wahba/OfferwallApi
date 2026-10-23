using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Results;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Features.Admin.SupportTickets;

public static class GetOpenSupportTickets
{
    // 1. Query (بدون UserId)
    public sealed record Query(
        int PageNumber = 1,
        int PageSize = 10
    ) : IQuery<Result<Response>>;

    // 2. Response DTOs
    public sealed record Response(
        int TotalCount,
        List<SupportTicketItemDto> Tickets
    );

    public sealed record SupportTicketItemDto(
        Guid TicketId,
        string DisplayTicketId,
        string UserId,
        string DisplayUserId,
        string Subject,
        string Category,
        DateTime SubmittedDate,
        string Status
    );

    // 3. Validator
    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
            RuleFor(x => x.PageSize).GreaterThan(0);
        }
    }

    // 4. Handler
    [Authorize(Role = "Admin")]
    internal sealed class Handler(ApplicationDbContext dbContext)
        : IQueryHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            // تصفية التذاكر المفتوحة فقط (Open) لجميع المستخدمين
            var query = dbContext.SupportTickets
                .AsNoTracking()
                .Where(st => st.Status == TicketStatus.Open);

            int totalCount = await query.CountAsync(cancellationToken);

            var pagedTickets = await query
                .OrderByDescending(st => st.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(st => new SupportTicketItemDto(
                    TicketId: st.TicketId,
                    DisplayTicketId: $"#TK-{st.TicketId.ToString().Substring(0, 4).ToUpper()}",
                    UserId: st.UserId.ToString(),
                    DisplayUserId: $"#USR-{st.UserId.ToString().Substring(0, 5).ToUpper()}",
                    Subject: st.Subject,
                    Category: st.Category.ToString().ToUpper(),
                    SubmittedDate: st.CreatedAt,
                    Status: st.Status.ToString().ToUpper()
                ))
                .ToListAsync(cancellationToken);

            return new Response(totalCount, pagedTickets);
        }
    }

    // 5. Endpoint Definition
    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/admin/support-tickets/open", async (
                [FromQuery] int pageNumber,
                [FromQuery] int pageSize,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                pageNumber = pageNumber > 0 ? pageNumber : 1;
                pageSize = pageSize > 0 ? pageSize : 10;

                var query = new Query(pageNumber, pageSize);
                var result = await sender.Send(query, cancellationToken);

                return result.ToHttpResponse();
            })
            .RequireAuthorization();
        }
    }
}