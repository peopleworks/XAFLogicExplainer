using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Archive.Module.BusinessObjects;

/// <summary>
/// A hand-made backup of the customer, kept beside it. <c>**\*.Backup.cs</c> removes it.
/// </summary>
public class CustomerBackup : BaseObject
{
    public CustomerBackup(Session session) : base(session) { }

    private string _name;

    public string Name
    {
        get => _name;
        set => SetPropertyValue(nameof(Name), ref _name, value);
    }
}
