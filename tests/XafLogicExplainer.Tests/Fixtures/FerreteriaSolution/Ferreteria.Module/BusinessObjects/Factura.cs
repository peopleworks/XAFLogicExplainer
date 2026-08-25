using DevExpress.Persistent.Base;
using DevExpress.Xpo;

namespace Ferreteria.Module.BusinessObjects;

[DefaultClassOptions]
public class Factura : AuditedEntity
{
    public Factura(Session session) : base(session) { }

    [Size(20)]
    public string Codigo { get; set; }

    public Cliente Cliente { get; set; }

    public decimal Total { get; set; }

    [Association("Factura-Detalles")]
    public XPCollection<FacturaDetalle> Detalles => GetCollection<FacturaDetalle>(nameof(Detalles));
}
