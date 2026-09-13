using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Archive.Module.BusinessObjects.Catalog;

/// <summary>
/// An earlier invoice left in the folder under another namespace. The project file removes it by
/// path, so the application has one <c>Invoice</c>, not two.
/// </summary>
[DefaultClassOptions]
public class Invoice : BaseObject
{
    public Invoice(Session session) : base(session) { }

    private string _reference;

    public string Reference
    {
        get => _reference;
        set => SetPropertyValue(nameof(Reference), ref _reference, value);
    }
}
