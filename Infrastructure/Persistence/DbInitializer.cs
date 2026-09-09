using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task SeedAdminAsync(
        ApplicationDbContext dbContext,
        IPasswordHasher passwordHasher)
    {

        if (await dbContext.Admins.AnyAsync())
            return;

        var hashResult = passwordHasher.Hash("Admin@123456");

        if (hashResult.IsFailure)
            throw new Exception($"Failed to hash initial admin password: {hashResult.Error.Description}");

        var admin = new Admin
        {
            AdminId = Guid.CreateVersion7(),
            Email = "tars.pixel@gmail.com",
            PasswordHash = hashResult.Value,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Admins.Add(admin);
        await dbContext.SaveChangesAsync();
    }
}