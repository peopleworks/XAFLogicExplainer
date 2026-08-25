using DevExpress.Persistent.Base;
using DevExpress.Xpo;

namespace Taller.Module.BusinessObjects;

[DefaultClassOptions]
public class Cliente : AuditedEntity
{
    public Cliente(Session session) : base(session) { }

    [Size(20)]
    [Indexed(Unique = true)]
    public string Codigo { get; set; }

    [Size(120)]
    public string Nombre { get; set; }

    [Size(20)]
    public string Telefono { get; set; }

    [Association("Cliente-Vehiculos")]
    public XPCollection<Vehiculo> Vehiculos => GetCollection<Vehiculo>(nameof(Vehiculos));
}
