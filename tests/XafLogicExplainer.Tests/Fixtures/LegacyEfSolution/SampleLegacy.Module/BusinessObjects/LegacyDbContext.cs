using DevExpress.ExpressApp.EFCore.Updating;
using DevExpress.Persistent.BaseImpl.EF;
using Microsoft.EntityFrameworkCore;

namespace SampleLegacy.Module.BusinessObjects;

/// <summary>
/// The application's own statement of what its entities are.
/// </summary>
/// <remarks>
/// Two of these types are supplied by the framework and declared nowhere in this project;
/// only <see cref="Invoice"/>, <see cref="Warehouse"/> and <see cref="Shipment"/> are the
/// application's own.
/// </remarks>
public class LegacyDbContext : DbContext
{
    public LegacyDbContext(DbContextOptions<LegacyDbContext> options) : base(options) { }

    public DbSet<ModuleInfo> ModulesInfo { get; set; }

    public DbSet<FileData> FileData { get; set; }

    public DbSet<Invoice> Invoices { get; set; }

    public DbSet<Warehouse> Warehouses { get; set; }

    public DbSet<Shipment> Shipments { get; set; }
}
