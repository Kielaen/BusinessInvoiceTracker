using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Invoice.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public static IServiceCollection AddInvoicePersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var boundedConnection = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Timeout = 5,
            CommandTimeout = 10,
            CancellationTimeout = 2000
        }.ConnectionString;
        services.AddScoped<PostgreSqlInvoiceNumberAllocator>();
        return services.AddDbContext<InvoiceDbContext>(
            options => options.UseNpgsql(
                boundedConnection,
                npgsql => npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(2),
                    errorCodesToAdd: null)));
    }
}
