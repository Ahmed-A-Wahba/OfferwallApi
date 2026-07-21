namespace OfferwallApi.Infrastructure.Interfaces;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    string Email { get; }

    string Role { get; }
}