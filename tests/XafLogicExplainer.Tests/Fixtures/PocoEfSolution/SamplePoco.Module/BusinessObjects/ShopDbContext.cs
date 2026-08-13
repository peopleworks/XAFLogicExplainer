using Microsoft.EntityFrameworkCore;

namespace SamplePoco.Module.BusinessObjects;

/// <summary>
/// An EF Core context that names no DevExpress type at all.
/// </summary>
/// <remarks>
/// An application on an existing schema has no reason to reference the DevExpress EF base
/// implementation: it does not use <c>BaseObject</c>, and the security tables may well live in
/// another project. Nothing here names DevExpress at all — and it is still an EF Core
/// application, which the DbContext states plainly.
/// <para>
/// The namespace this fixture must not name is deliberately absent from the comments too: the
/// detector it exercises reads raw file text, so writing it here would be enough to pass.
/// </para>
/// </remarks>
public class ShopDbContext : DbContext
{
    public ShopDbContext(DbContextOptions<ShopDbContext> options) : base(options) { }

    public DbSet<Coupon> Coupons { get; set; }
}
