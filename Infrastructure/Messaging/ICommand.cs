using MediatR;
using OfferwallApi.Infrastructure.Results;

namespace OfferwallApi.Infrastructure.Messaging;

public interface ICommand : IRequest
{
}

public interface ICommand<out TResponse> : IRequest<TResponse>
{
}
