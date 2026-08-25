using DevExpress.Xpo;

namespace Ferreteria.Module.BusinessObjects;

public class FacturaDetalle : AuditedEntity
{
    public FacturaDetalle(Session session) : base(session) { }

    [Association("Factura-Detalles")]
    public Factura Factura { get; set; }

    public Producto Producto { get; set; }

    public decimal Cantidad { get; set; }
}
