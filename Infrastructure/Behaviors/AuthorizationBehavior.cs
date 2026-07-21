using MediatR;
using OfferwallApi.Infrastructure.Authorization;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Messaging;
using OfferwallApi.Infrastructure.Results;
using System.Reflection;

namespace OfferwallApi.Infrastructure.Behaviors;

public class AuthorizationBehavior<TRequest, TResponse>(ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse>
        where TRequest : ICommand<TResponse>
        where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var authorizationAttributes = request.GetType()
            .GetCustomAttributes<AuthorizeAttribute>()
            .ToList();

        if (authorizationAttributes.Count == 0)
        {
            return await next();
        }


        var requiredRoles = authorizationAttributes
            .Select(attr => attr.Role)
            .Where(role => !string.IsNullOrEmpty(role))
            .ToList();

        if (!requiredRoles.Contains(currentUser.Role))
        {
            return (dynamic)Error.Unauthorized("Unauthorized", "User is forbidden from taking this action");
        }

        return await next();
    }
}