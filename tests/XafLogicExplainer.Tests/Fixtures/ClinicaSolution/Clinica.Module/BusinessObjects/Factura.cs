using DevExpress.Persistent.Base;
using DevExpress.Xpo;

namespace Clinica.Module.BusinessObjects;

[DefaultClassOptions]
public class Factura : AuditedEntity
{
    public Factura(Session session) : base(session) { }

    [Size(20)]
    public string Codigo { get; set; }

    [Association("Cliente-Facturas")]
    public Cliente Cliente { get; set; }

    public decimal Total { get; set; }

    public DateTime Fecha { get; set; }
}
