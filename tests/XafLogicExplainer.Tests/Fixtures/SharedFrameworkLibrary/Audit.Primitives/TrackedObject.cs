using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Audit.Primitives;

/// <summary>
/// The root of the shared hierarchy, two projects away from any application that uses it.
/// </summary>
/// <remarks>
/// It sits at the top of its project rather than in a BusinessObjects folder, which is how a
/// primitives library is usually laid out and is the reason a referenced project has to be read
/// whole rather than only where a module keeps its entities.
/// </remarks>
public abstract class TrackedObject : BaseObject
{
    protected TrackedObject(Session session) : base(session) { }

    public DateTime CreatedOn { get; set; }
}
