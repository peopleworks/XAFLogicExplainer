using Microsoft.EntityFrameworkCore;

namespace Shapes.Data;

/// <summary>
/// A second context outside the business-object folder, which discovery never reaches.
/// </summary>
public class SideContext : DbContext
{
    public DbSet<Shapes.BusinessObjects.Zeta> Zetas { get; set; }
}
