using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Library.Module.BusinessObjects;

/// <summary>
/// A label a reader puts on a book. Shares its name with the catalog's <c>Tag</c>, and keeps the
/// view ids XAF derives from the name.
/// </summary>
[DefaultClassOptions]
public class Tag : BaseObject
{
    public Tag(Session session) : base(session) { }

    private string _name;

    public string Name
    {
        get => _name;
        set => SetPropertyValue(nameof(Name), ref _name, value);
    }

    [Association("Tag-Books")]
    public XPCollection<Book> Books => GetCollection<Book>(nameof(Books));
}
