using Microsoft.EntityFrameworkCore;

namespace SampleLegacy.Module.BusinessObjects;

/// <summary>
/// A helper that reaches for a set the context never declares as a property.
/// </summary>
/// <remarks>
/// <c>DbSet&lt;T&gt;</c> written as a local is not the application saying "this is one of my
/// tables" -- it is a type name in a method body. Reading every mention of the generic instead of
/// the context's own properties picks this up as a registration.
/// </remarks>
public class LegacyRepository
{
    public int CountAudits(DbContext database)
    {
        DbSet<AuditEntry> entries = database.Set<AuditEntry>();
        return entries.Count();
    }
}

/// <summary>A row this module reads but does not own or map.</summary>
public class AuditEntry
{
    public int Id { get; set; }

    public string? Message { get; set; }
}
