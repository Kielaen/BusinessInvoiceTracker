using Invoice.Domain.Businesses;
using Invoice.Infrastructure.Identity;
using Invoice.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Invoice.Api.Identity;

public static class DemonstrationDataSeeder
{
    private sealed record DemoUser(
        Guid Id,
        string Email,
        Guid? BusinessId,
        BusinessRole? Role,
        bool IsActive = true);

    private static readonly DemoUser[] Users =
    [
        new(
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1"),
            "owner@acme.test",
            DemoSeedIds.AcmeBusiness,
            BusinessRole.Owner),
        new(
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa2"),
            "accountant@acme.test",
            DemoSeedIds.AcmeBusiness,
            BusinessRole.Accountant),
        new(
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa3"),
            "viewer@acme.test",
            DemoSeedIds.AcmeBusiness,
            BusinessRole.Viewer),
        new(
            Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1"),
            "owner@other.test",
            DemoSeedIds.OtherBusiness,
            BusinessRole.Owner),
        new(
            Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1"),
            "nomembership@test.local",
            null,
            null),
        new(
            Guid.Parse("dddddddd-dddd-4ddd-8ddd-ddddddddddd1"),
            "disabled@acme.test",
            DemoSeedIds.AcmeBusiness,
            BusinessRole.Accountant,
            IsActive: false)
    ];

    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        IHostEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("DemoSeed:Enabled"))
        {
            return;
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Demonstration identity seeding is permitted only in Development.");
        }

        var password = configuration["DemoSeed:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "DemoSeed:Password is required when demonstration seeding is enabled.");
        }

        var database = services.GetRequiredService<InvoiceDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        await EnsureBusinessAsync(
            database,
            DemoSeedIds.AcmeBusiness,
            "Acme Demonstration Business",
            cancellationToken);
        await EnsureBusinessAsync(
            database,
            DemoSeedIds.OtherBusiness,
            "Other Demonstration Business",
            cancellationToken);

        foreach (var demo in Users)
        {
            var user = await userManager.FindByEmailAsync(demo.Email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = demo.Id,
                    Email = demo.Email,
                    UserName = demo.Email,
                    EmailConfirmed = true,
                    LockoutEnabled = true
                };
                var result = await userManager.CreateAsync(user, password);
                if (!result.Succeeded)
                {
                    var errors = string.Join(
                        ", ",
                        result.Errors.Select(error => error.Code));
                    throw new InvalidOperationException(
                        $"Could not create demonstration user {demo.Email}: {errors}");
                }
            }

            if (demo.BusinessId is null || demo.Role is null)
            {
                continue;
            }

            var membership = await database.BusinessMemberships.FindAsync(
                [demo.BusinessId.Value, user.Id],
                cancellationToken);
            if (membership is null)
            {
                database.BusinessMemberships.Add(new BusinessMembership
                {
                    BusinessId = demo.BusinessId.Value,
                    UserId = user.Id,
                    Role = demo.Role.Value,
                    IsActive = demo.IsActive,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                });
            }
            else
            {
                membership.Role = demo.Role.Value;
                membership.IsActive = demo.IsActive;
            }
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureBusinessAsync(
        InvoiceDbContext database,
        Guid id,
        string name,
        CancellationToken cancellationToken)
    {
        if (await database.Businesses.AnyAsync(
            business => business.Id == id,
            cancellationToken))
        {
            return;
        }

        database.Businesses.Add(new Business
        {
            Id = id,
            Name = name,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
    }
}
