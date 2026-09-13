using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Library.Module.BusinessObjects.Catalog;

/// <summary>
/// One line of a catalog genre. Its <c>Tag</c> is written bare, and C# reads a bare name in the
/// namespace it is written in first: this is the catalog's <c>Tag</c>, not the reader's.
/// </summary>
public class Entry : BaseObject
{
    public Entry(Session session) : base(session) { }

    private string _text;

    public string Text
    {
        get => _text;
        set => SetPropertyValue(nameof(Text), ref _text, value);
    }

    private Tag _tag;

    [Association("Tag-Entries")]
    public Tag Tag
    {
        get => _tag;
        set => SetPropertyValue(nameof(Tag), ref _tag, value);
    }
}
