using DevExpress.Persistent.Base;
using DevExpress.Xpo;

namespace Taller.Module.BusinessObjects;

[DefaultClassOptions]
public class Vehiculo : AuditedEntity
{
    public Vehiculo(Session session) : base(session) { }

    [Size(12)]
    public string Placa { get; set; }

    [Association("Cliente-Vehiculos")]
    public Cliente Cliente { get; set; }

    [Size(60)]
    public string Modelo { get; set; }
}
