using DevExpress.Persistent.Base;
using DevExpress.Xpo;

namespace Taller.Module.BusinessObjects;

[DefaultClassOptions]
public class Factura : AuditedEntity
{
    public Factura(Session session) : base(session) { }

    [Size(20)]
    public string Codigo { get; set; }

    public Cliente Cliente { get; set; }

    // The divergence the corpus is built to catch: the same money, a different type.
    public double Total { get; set; }

    public DateTime Fecha { get; set; }
}
