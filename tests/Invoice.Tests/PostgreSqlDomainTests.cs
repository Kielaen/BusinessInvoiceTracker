using Invoice.Domain.Businesses;
using Invoice.Domain.Common;
using Invoice.Domain.Customers;
using Invoice.Domain.Financial;
using Invoice.Domain.Invoices;
using Invoice.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Invoice.Tests;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class PostgreSqlDomainTests(PostgreSqlFixture fixture)
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 31, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ConcurrentAllocatorIsUniqueMonotonicAndTenantIndependent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.ResetToLatestAsync(cancellationToken);
        var firstBusinessId = Guid.NewGuid();
        var secondBusinessId = Guid.NewGuid();
        await using (var setup = fixture.CreateContext())
        {
            setup.Businesses.AddRange(
                CreateBusiness(firstBusinessId, "First"),
                CreateBusiness(secondBusinessId, "Second"));
            await setup.SaveChangesAsync(cancellationToken);
        }

        var allocations = await Task.WhenAll(
            Enumerable.Range(0, 40).Select(async _ =>
            {
                await using var database = fixture.CreateContext();
                var allocator = new PostgreSqlInvoiceNumberAllocator(database);
                return await allocator.AllocateAsync(
                    firstBusinessId,
                    cancellationToken);
            }));

        var sequences = allocations
            .Select(number => number.Sequence)
            .Order()
            .ToArray();
        Assert.Equal(Enumerable.Range(1, 40).Select(value => (long)value), sequences);
        Assert.Equal(40, allocations.Select(number => number.Value).Distinct().Count());

        await using var secondDatabase = fixture.CreateContext();
        var secondAllocator = new PostgreSqlInvoiceNumberAllocator(secondDatabase);
        var secondFirst = await secondAllocator.AllocateAsync(
            secondBusinessId,
            cancellationToken);
        Assert.Equal(1, secondFirst.Sequence);
    }

    [Fact]
    public async Task StaleInvoiceWriterReceivesConcurrencyConflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.ResetToLatestAsync(cancellationToken);
        var businessId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        await using (var setup = fixture.CreateContext())
        {
            setup.Businesses.Add(CreateBusiness(businessId, "Concurrency"));
            setup.Customers.Add(Customer.Create(
                customerId,
                businessId,
                "Customer",
                EmailAddress.Parse("customer@example.test"),
                "+27 11 555 0100",
                "1 Main Road",
                null,
                "Johannesburg",
                "2000",
                "ZA",
                Now));
            var invoice = CreateInvoice(invoiceId, businessId, customerId);
            setup.Invoices.Add(invoice);
            await setup.SaveChangesAsync(cancellationToken);
        }

        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext();
        var firstInvoice = await first.Invoices.SingleAsync(
            invoice => invoice.Id == invoiceId,
            cancellationToken);
        var secondInvoice = await second.Invoices.SingleAsync(
            invoice => invoice.Id == invoiceId,
            cancellationToken);
        firstInvoice.Void(Now.AddMinutes(1));
        secondInvoice.Void(Now.AddMinutes(2));

        await first.SaveChangesAsync(cancellationToken);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => second.SaveChangesAsync(cancellationToken));
    }

    [Fact]
    public async Task MigrationAppliesFromAllSupportedSnapshots()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using (var empty = fixture.CreateContext())
        {
            await empty.Database.EnsureDeletedAsync(cancellationToken);
            await empty.Database.MigrateAsync(cancellationToken);
            Assert.True(await empty.Database.CanConnectAsync(cancellationToken));
            Assert.Contains(
                "AddDashboardQueryIndexes",
                (await empty.Database.GetAppliedMigrationsAsync(
                    cancellationToken)).Last());
        }

        await using (var previous = fixture.CreateContext())
        {
            await previous.Database.EnsureDeletedAsync(cancellationToken);
            var migrator = previous.GetService<IMigrator>();
            await migrator.MigrateAsync(
                "20260731083706_AddIdentityAndTenancy",
                cancellationToken);
            Assert.DoesNotContain(
                "AddFinancialDomain",
                await previous.Database.GetAppliedMigrationsAsync(
                    cancellationToken));
            await migrator.MigrateAsync(cancellationToken: cancellationToken);
            Assert.Contains(
                "AddDashboardQueryIndexes",
                (await previous.Database.GetAppliedMigrationsAsync(
                    cancellationToken)).Last());
        }

        await using (var phaseThree = fixture.CreateContext())
        {
            await phaseThree.Database.EnsureDeletedAsync(cancellationToken);
            var migrator = phaseThree.GetService<IMigrator>();
            await migrator.MigrateAsync(
                "20260731093641_AddFinancialDomain",
                cancellationToken);
            Assert.DoesNotContain(
                "AddDashboardQueryIndexes",
                await phaseThree.Database.GetAppliedMigrationsAsync(
                    cancellationToken));
            await migrator.MigrateAsync(cancellationToken: cancellationToken);
            Assert.Contains(
                "AddDashboardQueryIndexes",
                (await phaseThree.Database.GetAppliedMigrationsAsync(
                    cancellationToken)).Last());
        }
    }

    [Fact]
    public async Task CompositeForeignKeyRejectsCrossTenantCustomer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.ResetToLatestAsync(cancellationToken);
        var customerBusinessId = Guid.NewGuid();
        var invoiceBusinessId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        await using var database = fixture.CreateContext();
        database.Businesses.AddRange(
            CreateBusiness(customerBusinessId, "Customer tenant"),
            CreateBusiness(invoiceBusinessId, "Invoice tenant"));
        database.Customers.Add(Customer.Create(
            customerId,
            customerBusinessId,
            "Customer",
            EmailAddress.Parse("tenant@example.test"),
            null,
            "1 Main Road",
            null,
            "Johannesburg",
            "2000",
            "ZA",
            Now));
        await database.SaveChangesAsync(cancellationToken);
        database.Invoices.Add(CreateInvoice(
            Guid.NewGuid(),
            invoiceBusinessId,
            customerId));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.SaveChangesAsync(cancellationToken));
    }

    [Fact]
    public async Task StaleCustomerWriterReceivesConcurrencyConflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.ResetToLatestAsync(cancellationToken);
        var businessId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        await using (var setup = fixture.CreateContext())
        {
            setup.Businesses.Add(CreateBusiness(businessId, "Customer concurrency"));
            setup.Customers.Add(CreateCustomer(customerId, businessId, "Concurrent customer"));
            await setup.SaveChangesAsync(cancellationToken);
        }

        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext();
        var firstCustomer = await first.Customers.SingleAsync(
            customer => customer.Id == customerId,
            cancellationToken);
        var secondCustomer = await second.Customers.SingleAsync(
            customer => customer.Id == customerId,
            cancellationToken);
        UpdateCustomer(firstCustomer, "First writer", "first@example.test");
        UpdateCustomer(secondCustomer, "Second writer", "second@example.test");

        await first.SaveChangesAsync(cancellationToken);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            second.SaveChangesAsync(cancellationToken));
    }

    [Fact]
    public async Task CustomerEmailIsUniquePerBusinessButReusableAcrossBusinesses()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.ResetToLatestAsync(cancellationToken);
        var firstBusiness = Guid.NewGuid();
        var secondBusiness = Guid.NewGuid();
        await using var database = fixture.CreateContext();
        database.Businesses.AddRange(
            CreateBusiness(firstBusiness, "First business"),
            CreateBusiness(secondBusiness, "Second business"));
        database.Customers.AddRange(
            CreateCustomer(Guid.NewGuid(), firstBusiness, "First customer"),
            CreateCustomer(Guid.NewGuid(), secondBusiness, "Second customer"));
        await database.SaveChangesAsync(cancellationToken);

        database.Customers.Add(
            CreateCustomer(Guid.NewGuid(), firstBusiness, "Duplicate customer"));
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            database.SaveChangesAsync(cancellationToken));
    }

    private static Business CreateBusiness(Guid id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            CreatedAtUtc = Now
        };

    private static Domain.Invoices.Invoice CreateInvoice(
        Guid id,
        Guid businessId,
        Guid customerId)
    {
        var zar = CurrencyCode.Parse("ZAR");
        var invoice = Domain.Invoices.Invoice.CreateDraft(
            id,
            businessId,
            customerId,
            zar,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            Now);
        invoice.ReplaceLines(
        [
            new("Service", 1m, Money.Create(100m, zar), TaxRate.Create(15m))
        ],
        Now);
        return invoice;
    }

    private static Customer CreateCustomer(
        Guid customerId,
        Guid businessId,
        string name) => Customer.Create(
            customerId,
            businessId,
            name,
            EmailAddress.Parse("shared@example.test"),
            null,
            "1 Main Road",
            null,
            "Johannesburg",
            "2000",
            "ZA",
            Now);

    private static void UpdateCustomer(
        Customer customer,
        string name,
        string email) => customer.Update(
            name,
            EmailAddress.Parse(email),
            null,
            "1 Main Road",
            null,
            "Johannesburg",
            "2000",
            "ZA",
            Now.AddMinutes(1));
}
