using Audit.Core;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;

namespace Client.Module.BusinessObjects;

[DefaultClassOptions]
public class Factura : AuditedEntity
{
    public Factura(Session session) : base(session) { }

    public decimal Total { get; set; }

    [Association("Cliente-Facturas")]
    public Cliente Cliente { get; set; }
}
