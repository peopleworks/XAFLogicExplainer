using DevExpress.Persistent.Base;
using DevExpress.Xpo;

namespace Ferreteria.Module.BusinessObjects;

[DefaultClassOptions]
public class Cliente : AuditedEntity
{
    public Cliente(Session session) : base(session) { }

    [Size(20)]
    [Indexed(Unique = true)]
    public string Codigo { get; set; }

    [Size(120)]
    public string Nombre { get; set; }

    public decimal LimiteCredito { get; set; }

    // Same name, a different shape: a flag here, free text next door.
    [Size(10)]
    public string Activo { get; set; }
}
