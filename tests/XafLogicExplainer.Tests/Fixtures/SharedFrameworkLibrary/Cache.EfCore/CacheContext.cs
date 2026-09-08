using Microsoft.EntityFrameworkCore;

namespace Cache.EfCore;

/// <summary>A row this utility caches. It belongs to the utility, not to any application.</summary>
public class CacheRow
{
    public int Id { get; set; }

    public string Payload { get; set; }
}

/// <summary>
/// A cache store on EF Core, referenced by applications that persist with something else.
/// </summary>
/// <remarks>
/// Caching, telemetry and an Identity database beside XAF security are all real reasons for an
/// XPO application to reference an EF Core project. None of them makes the application EF Core.
/// </remarks>
public class CacheContext : DbContext
{
    public CacheContext(DbContextOptions<CacheContext> options) : base(options) { }

    public virtual DbSet<CacheRow> Rows { get; set; }
}
