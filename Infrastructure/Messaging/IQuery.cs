using MediatR;

namespace OfferwallApi.Infrastructure.Messaging;

public interface IQuery<out TResponse> : IRequest<TResponse>
{
}