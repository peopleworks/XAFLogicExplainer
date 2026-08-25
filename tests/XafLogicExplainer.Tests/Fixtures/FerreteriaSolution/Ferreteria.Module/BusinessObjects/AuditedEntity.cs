using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Ferreteria.Module.BusinessObjects;

/// <summary>Everything this developer builds carries who touched it and when.</summary>
/// <remarks>Copied from the last client, which is how it usually travels.</remarks>
public abstract class AuditedEntity : BaseObject
{
    public AuditedEntity(Session session) : base(session) { }

    [Size(60)]
    public string CreadoPor { get; set; }

    public DateTime CreadoEl { get; set; }

    public DateTime ModificadoEl { get; set; }
}
