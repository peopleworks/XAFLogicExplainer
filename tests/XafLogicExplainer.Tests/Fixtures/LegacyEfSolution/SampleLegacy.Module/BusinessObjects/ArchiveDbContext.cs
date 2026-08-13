using Microsoft.EntityFrameworkCore;

namespace SampleLegacy.Module.BusinessObjects;

/// <summary>
/// The context every other context in this application derives from.
/// </summary>
/// <remarks>
/// A shared base is how an application puts auditing, soft delete or a connection convention in
/// one place. It means the contexts that actually register entities do not name
/// <c>DbContext</c> anywhere in their own declaration.
/// </remarks>
public abstract class AuditedDbContext : DbContext
{
    protected AuditedDbContext(DbContextOptions options) : base(options) { }

    public override int SaveChanges() => base.SaveChanges();
}

/// <summary>
/// Closed invoices, moved off the live tables. Registers through the shared base.
/// </summary>
public class ArchiveDbContext : AuditedDbContext
{
    public ArchiveDbContext(DbContextOptions<ArchiveDbContext> options) : base(options) { }

    public DbSet<ArchivedInvoice> ArchivedInvoices { get; set; }
}
