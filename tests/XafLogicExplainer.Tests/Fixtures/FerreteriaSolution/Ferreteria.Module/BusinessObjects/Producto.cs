using DevExpress.Persistent.Base;
using DevExpress.Xpo;

namespace Ferreteria.Module.BusinessObjects;

[DefaultClassOptions]
public class Producto : AuditedEntity
{
    public Producto(Session session) : base(session) { }

    [Size(20)]
    public string Codigo { get; set; }

    [Size(120)]
    public string Nombre { get; set; }

    public decimal Precio { get; set; }
}
