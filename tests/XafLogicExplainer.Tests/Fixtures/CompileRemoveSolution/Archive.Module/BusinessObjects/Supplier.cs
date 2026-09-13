using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Archive.Module.BusinessObjects;

/// <summary>
/// A supplier. The project file removes it only in the <c>Legacy</c> configuration, so every other
/// build compiles it.
/// </summary>
[DefaultClassOptions]
public class Supplier : BaseObject
{
    public Supplier(Session session) : base(session) { }

    private string _name;

    public string Name
    {
        get => _name;
        set => SetPropertyValue(nameof(Name), ref _name, value);
    }
}
