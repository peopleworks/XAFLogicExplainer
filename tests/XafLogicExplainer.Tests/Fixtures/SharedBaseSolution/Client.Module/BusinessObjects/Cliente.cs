using Audit.Core;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;

namespace Client.Module.BusinessObjects;

[DefaultClassOptions]
[NavigationItem("Ventas")]
public class Cliente : AuditedEntity
{
    public Cliente(Session session) : base(session) { }

    [Size(100)]
    public string Nombre { get; set; }

    [Size(11)]
    public string Rnc { get; set; }
}
