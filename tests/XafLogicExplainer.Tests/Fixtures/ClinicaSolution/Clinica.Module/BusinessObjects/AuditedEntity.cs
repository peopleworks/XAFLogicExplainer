using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Clinica.Module.BusinessObjects;

/// <summary>Everything this developer builds carries who touched it and when.</summary>
public abstract class AuditedEntity : BaseObject
{
    public AuditedEntity(Session session) : base(session) { }

    [Size(60)]
    public string CreadoPor { get; set; }

    public DateTime CreadoEl { get; set; }
}
