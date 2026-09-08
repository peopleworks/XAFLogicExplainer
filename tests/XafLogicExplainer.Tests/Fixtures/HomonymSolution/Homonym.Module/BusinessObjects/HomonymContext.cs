using Microsoft.EntityFrameworkCore;

namespace Homonym.Module.Data;

public class HomonymContext : DbContext
{
    public HomonymContext(DbContextOptions<HomonymContext> options) : base(options) { }

    public virtual DbSet<Homonym.Core.Cliente> Clientes { get; set; }
}
