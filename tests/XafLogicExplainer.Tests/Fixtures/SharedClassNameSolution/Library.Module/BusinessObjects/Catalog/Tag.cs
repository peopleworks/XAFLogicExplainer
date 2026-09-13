using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Library.Module.BusinessObjects.Catalog;

/// <summary>
/// A genre in the library's catalog. The second class named <c>Tag</c>; <c>LibraryModule</c> gives
/// its views the <c>CatalogTag</c> prefix.
/// </summary>
[DefaultClassOptions]
public class Tag : BaseObject
{
    public Tag(Session session) : base(session) { }

    private string _code;

    public string Code
    {
        get => _code;
        set => SetPropertyValue(nameof(Code), ref _code, value);
    }

    [Association("Book-Genres")]
    public XPCollection<Book> Books => GetCollection<Book>(nameof(Books));

    [Aggregated]
    [Association("Tag-Entries")]
    public XPCollection<Entry> Entries => GetCollection<Entry>(nameof(Entries));
}
