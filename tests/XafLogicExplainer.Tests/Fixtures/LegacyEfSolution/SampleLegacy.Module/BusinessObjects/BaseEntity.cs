using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;

namespace SampleLegacy.Module.BusinessObjects;

/// <summary>
/// The base class an application writes for itself when its tables already exist.
/// </summary>
/// <remarks>
/// It carries the XAF contract through interfaces rather than deriving from
/// <c>BaseObject</c>, because a legacy table brings its own primary key and the
/// <c>Oid</c> that <c>BaseObject</c> insists on does not fit it.
/// </remarks>
public abstract class BaseEntity : IXafEntityObject, IObjectSpaceLink
{
    protected IObjectSpace ObjectSpace;

    IObjectSpace IObjectSpaceLink.ObjectSpace
    {
        get => ObjectSpace;
        set => ObjectSpace = value;
    }

    public virtual void OnCreated() { }

    public virtual void OnLoaded() { }

    public virtual void OnSaving() { }
}
