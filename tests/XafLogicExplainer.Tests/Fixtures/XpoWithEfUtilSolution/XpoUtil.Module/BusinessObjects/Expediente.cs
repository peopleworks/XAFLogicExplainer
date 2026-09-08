using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace XpoUtil.Module.BusinessObjects;

[DefaultClassOptions]
[NavigationItem("Casos")]
public class Expediente : BaseObject
{
    public Expediente(Session session) : base(session) { }

    [Size(30)]
    public string Numero { get; set; }

    public DateTime Abierto { get; set; }
}
