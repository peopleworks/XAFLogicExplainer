using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Ledger.Module.BusinessObjects;

/// <summary>
/// A ledger account, listed in both project files.
/// </summary>
[DefaultClassOptions]
public class Account : BaseObject
{
    public Account(Session session) : base(session) { }

    private string _code;

    public string Code
    {
        get => _code;
        set => SetPropertyValue(nameof(Code), ref _code, value);
    }
}
