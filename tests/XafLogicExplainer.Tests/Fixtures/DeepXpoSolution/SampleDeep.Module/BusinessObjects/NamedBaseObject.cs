using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace SampleDeep.Module.BusinessObjects;

/// <summary>
/// The shared base an application writes once and derives everything from.
/// </summary>
/// <remarks>
/// This is the shape FeatureCenter uses throughout: one abstract class holding the convention,
/// and the real business objects two or three hops below it.
/// </remarks>
public abstract class NamedBaseObject : BaseObject
{
    public NamedBaseObject(Session session) : base(session) { }

    public string Name
    {
        get => GetPropertyValue<string>(nameof(Name));
        set => SetPropertyValue(nameof(Name), value);
    }
}
