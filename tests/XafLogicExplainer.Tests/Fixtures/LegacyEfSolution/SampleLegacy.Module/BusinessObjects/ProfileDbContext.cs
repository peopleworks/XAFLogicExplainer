using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SampleLegacy.Module.BusinessObjects;

/// <summary>
/// A context whose base class comes from a package, not from this source tree.
/// </summary>
/// <remarks>
/// <c>IdentityDbContext&lt;TUser&gt;</c> is the base every ASP.NET Core Identity template writes,
/// and neither it nor its type argument is declared anywhere in this fixture -- which is the whole
/// point. Both other context shapes here name a base that can be read: <c>LegacyDbContext</c>
/// derives from <c>DbContext</c> directly, and <c>ArchiveDbContext</c> derives from an
/// <c>AuditedDbContext</c> declared one file over. A rule that reaches a context only by walking
/// to a base it can see passes both of those and registers nothing at all for this one.
/// </remarks>
public class ProfileDbContext : IdentityDbContext<AppUser>
{
    public ProfileDbContext(DbContextOptions<ProfileDbContext> options) : base(options) { }

    public virtual DbSet<UserProfile> Profiles { get; set; }
}

/// <summary>
/// A table reachable only through the context on the package base.
/// </summary>
[Table("user_profile")]
public partial class UserProfile
{
    [Key]
    [Column("Id")]
    public virtual int Id { get; set; }

    [Column("DisplayName")]
    [StringLength(80)]
    public virtual string DisplayName { get; set; }
}
