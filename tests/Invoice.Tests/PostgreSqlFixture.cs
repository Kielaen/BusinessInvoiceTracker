using Invoice.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Invoice.Tests;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private const string Image =
        "postgres:17.10-alpine3.23@sha256:" +
        "8189a1f6e40904781fc9e2612687877791d21679866db58b1de996b31fc312e4";

    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder(Image)
            .WithDatabase("invoice_tests")
            .WithUsername("invoice_tests")
            .WithPassword("invoice-tests-only")
            .Build();

    public string ConnectionString => _container.GetConnectionString() +
        ";GSS Encryption Mode=Disable";

    public InvoiceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<InvoiceDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new InvoiceDbContext(options);
    }

    public async Task ResetToLatestAsync(CancellationToken cancellationToken)
    {
        await using var database = CreateContext();
        await database.Database.EnsureDeletedAsync(cancellationToken);
        await database.Database.MigrateAsync(cancellationToken);
    }

    public ValueTask InitializeAsync() =>
        new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class PostgreSqlTestGroup
    : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "postgresql";
}
