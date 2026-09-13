using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Library.Module.BusinessObjects;

/// <summary>
/// A book, pointing at both classes named <c>Tag</c>: the one in its own namespace by the bare name,
/// and the catalog's by writing the namespace in front of it.
/// </summary>
[DefaultClassOptions]
public class Book : BaseObject
{
    public Book(Session session) : base(session) { }

    private string _title;

    public string Title
    {
        get => _title;
        set => SetPropertyValue(nameof(Title), ref _title, value);
    }

    private Tag _tag;

    [Association("Tag-Books")]
    public Tag Tag
    {
        get => _tag;
        set => SetPropertyValue(nameof(Tag), ref _tag, value);
    }

    [Association("Book-Genres")]
    public XPCollection<Catalog.Tag> Genres => GetCollection<Catalog.Tag>(nameof(Genres));
}
