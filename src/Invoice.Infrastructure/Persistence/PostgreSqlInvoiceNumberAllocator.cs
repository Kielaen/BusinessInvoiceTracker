using System.Data;
using Invoice.Domain.Common;
using Invoice.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Invoice.Infrastructure.Persistence;

public sealed class PostgreSqlInvoiceNumberAllocator(
    InvoiceDbContext database)
{
    private const string AllocationSql = """
        INSERT INTO invoice_number_sequences ("BusinessId", "NextValue")
        VALUES (@businessId, 2)
        ON CONFLICT ("BusinessId") DO UPDATE
        SET "NextValue" = invoice_number_sequences."NextValue" + 1
        WHERE invoice_number_sequences."NextValue" <= 99999999
        RETURNING "NextValue" - 1;
        """;

    public async Task<InvoiceNumber> AllocateAsync(
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        if (businessId == Guid.Empty)
        {
            throw new ArgumentException(
                "Business identifier is required.",
                nameof(businessId));
        }

        var connection = database.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = AllocationSql;
            command.Transaction = database.Database.CurrentTransaction
                ?.GetDbTransaction();
            var parameter = command.CreateParameter();
            parameter.ParameterName = "businessId";
            parameter.Value = businessId;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is null || result is DBNull)
            {
                throw new DomainRuleViolationException(
                    "invoice_number.exhausted",
                    "The business invoice-number range is exhausted.");
            }

            return InvoiceNumber.FromSequence(
                Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
