using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Shapes.BusinessObjects;

/// <summary>
/// Every way a real project spells a registration, in one class.
/// </summary>
/// <remarks>
/// None of these is exotic. The qualified property type is what a file with no <c>using</c> for
/// EF Core writes, <c>global::</c> is what a generator emits to be safe about ambiguity, and the
/// type argument on the base is the whole registration of the user table in the template ASP.NET
/// Core writes.
/// </remarks>
public class ShapesContext : IdentityDbContext<AppUser>
{
    public DbSet<Epsilon> Epsilons => Set<Epsilon>();

    public Microsoft.EntityFrameworkCore.DbSet<Alpha> Alphas { get; set; }

    public DbSet<global::Shapes.BusinessObjects.Beta> Betas { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // A real registration this reader does not follow: it is a call in a method body, not a
        // declaration with a shape.
        modelBuilder.Entity<Gamma>().ToTable("gamma");
    }
}
