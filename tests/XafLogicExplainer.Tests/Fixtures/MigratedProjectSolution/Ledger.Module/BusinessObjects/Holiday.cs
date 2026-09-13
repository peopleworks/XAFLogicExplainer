using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Ledger.Module.BusinessObjects;

/// <summary>
/// Added after the migration. The new SDK project compiles it by default, and the old project file,
/// which lists every file by hand, was never updated to name it.
/// </summary>
[DefaultClassOptions]
public class Holiday : BaseObject
{
    public Holiday(Session session) : base(session) { }

    private DateTime _date;

    public DateTime Date
    {
        get => _date;
        set => SetPropertyValue(nameof(Date), ref _date, value);
    }
}
