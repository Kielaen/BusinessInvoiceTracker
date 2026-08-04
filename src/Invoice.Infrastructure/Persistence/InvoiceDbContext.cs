using Invoice.Domain.Businesses;
using Invoice.Domain.Auditing;
using Invoice.Domain.Customers;
using Invoice.Domain.Invoices;
using Invoice.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Invoice.Infrastructure.Persistence;

public sealed class InvoiceDbContext(DbContextOptions<InvoiceDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<BusinessMembership> BusinessMemberships =>
        Set<BusinessMembership>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Domain.Invoices.Invoice> Invoices =>
        Set<Domain.Invoices.Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<InvoiceNumberSequence> InvoiceNumberSequences =>
        Set<InvoiceNumberSequence>();
    public DbSet<DeliveryProbe> DeliveryProbes => Set<DeliveryProbe>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<AutomationReceipt> AutomationReceipts => Set<AutomationReceipt>();
    public DbSet<WorkflowCommand> WorkflowCommands => Set<WorkflowCommand>();
    public DbSet<AutomationDeadLetter> AutomationDeadLetters =>
        Set<AutomationDeadLetter>();
    public DbSet<NotificationDelivery> NotificationDeliveries =>
        Set<NotificationDelivery>();
    public DbSet<NotificationAttempt> NotificationAttempts =>
        Set<NotificationAttempt>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Business>(entity =>
        {
            entity.ToTable("businesses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160);
        });

        builder.Entity<BusinessMembership>(entity =>
        {
            entity.ToTable("business_memberships");
            entity.HasKey(x => new { x.BusinessId, x.UserId });
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.UserId, x.IsActive });
            entity.HasOne(x => x.Business)
                .WithMany(x => x.Memberships)
                .HasForeignKey(x => x.BusinessId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DeliveryProbe>(entity =>
        {
            entity.ToTable("delivery_probes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RequestedBy).HasMaxLength(128);
        });

        builder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(256);
            entity.Property(x => x.Payload).HasColumnType("jsonb");
            entity.Property(x => x.LastError).HasMaxLength(1000);
            entity.HasIndex(
                x => new
                {
                    x.PublishedAtUtc,
                    x.NextAttemptAtUtc,
                    x.OccurredAtUtc
                });
        });

        builder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("inbox_messages");
            entity.HasKey(x => x.MessageId);
        });

        builder.Entity<AutomationReceipt>(entity =>
        {
            entity.ToTable("automation_receipts");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.MessageId).IsUnique();
            entity.HasIndex(x => x.ProbeId).IsUnique();
            entity.Property(x => x.RequestedBy).HasMaxLength(128);
        });

        builder.Entity<WorkflowCommand>(entity =>
        {
            entity.ToTable("workflow_commands");
            entity.HasKey(command => command.Id);
            entity.Property(command => command.Kind).HasMaxLength(32);
            entity.Property(command => command.Payload).HasColumnType("jsonb");
            entity.Property(command => command.LastError).HasMaxLength(1000);
            entity.HasIndex(command => command.EventMessageId).IsUnique();
            entity.HasIndex(command => new
            {
                command.DispatchedAtUtc,
                command.FailedAtUtc,
                command.NextAttemptAtUtc,
                command.LeaseUntilUtc
            });
            entity.HasIndex(command => new
            {
                command.InvoiceId,
                command.CreatedAtUtc
            });
        });

        builder.Entity<AutomationDeadLetter>(entity =>
        {
            entity.ToTable("automation_dead_letters");
            entity.HasKey(message => message.MessageId);
            entity.Property(message => message.MessageId).HasMaxLength(200);
            entity.Property(message => message.EventType).HasMaxLength(256);
            entity.Property(message => message.Payload).HasColumnType("jsonb");
            entity.HasIndex(message => new
            {
                message.ResolvedAtUtc,
                message.ReceivedAtUtc
            });
        });

        builder.Entity<NotificationDelivery>(entity =>
        {
            entity.ToTable("notification_deliveries");
            entity.HasKey(delivery => delivery.Id);
            entity.Property(delivery => delivery.WorkflowGeneration).HasMaxLength(32);
            entity.Property(delivery => delivery.Recipient).HasMaxLength(320);
            entity.Property(delivery => delivery.Status).HasMaxLength(32);
            entity.Property(delivery => delivery.ProviderMessageId).HasMaxLength(200);
            entity.Property(delivery => delivery.LastError).HasMaxLength(1000);
            entity.HasIndex(delivery => new
            {
                delivery.InvoiceId,
                delivery.WorkflowGeneration
            }).IsUnique();
            entity.HasIndex(delivery => new
            {
                delivery.Status,
                delivery.CreatedAtUtc
            });
        });

        builder.Entity<NotificationAttempt>(entity =>
        {
            entity.ToTable("notification_attempts");
            entity.HasKey(attempt => attempt.Id);
            entity.Property(attempt => attempt.Outcome).HasMaxLength(32);
            entity.Property(attempt => attempt.Error).HasMaxLength(1000);
            entity.HasIndex(attempt => new
            {
                attempt.DeliveryId,
                attempt.AttemptNumber
            }).IsUnique();
            entity.HasOne<NotificationDelivery>()
                .WithMany()
                .HasForeignKey(attempt => attempt.DeliveryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.ConfigureDomainModel();
    }
}
