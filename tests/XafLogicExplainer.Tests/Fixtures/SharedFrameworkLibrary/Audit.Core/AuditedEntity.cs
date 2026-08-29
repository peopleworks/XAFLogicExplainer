using Audit.Primitives;
using DevExpress.Xpo;

namespace Audit.Core;

/// <summary>
/// The base an application actually derives from. Declared here, never in the application.
/// </summary>
public abstract class AuditedEntity : TrackedObject
{
    protected AuditedEntity(Session session) : base(session) { }

    public string CreatedBy { get; set; }
}
