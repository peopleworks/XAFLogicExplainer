using DevExpress.Persistent.Base;
using DevExpress.Xpo;

namespace Clinica.Module.BusinessObjects;

[DefaultClassOptions]
public class Cliente : AuditedEntity
{
    public Cliente(Session session) : base(session) { }

    [Size(20)]
    [Indexed(Unique = true)]
    public string Codigo { get; set; }

    [Size(120)]
    public string Nombre { get; set; }

    [Size(40)]
    public string Seguro { get; set; }

    public bool Activo { get; set; }

    [Association("Cliente-Facturas")]
    public XPCollection<Factura> Facturas => GetCollection<Factura>(nameof(Facturas));
}
