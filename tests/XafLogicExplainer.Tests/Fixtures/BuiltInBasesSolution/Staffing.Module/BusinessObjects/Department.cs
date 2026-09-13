using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Staffing.Module.BusinessObjects;

/// <summary>
/// A department, on <c>BaseObject</c> — the end of <c>Department-Employees</c> that was always read.
/// </summary>
[DefaultClassOptions]
[NavigationItem("Staff")]
public class Department : BaseObject
{
    public Department(Session session) : base(session) { }

    private string _name;

    public string Name
    {
        get => _name;
        set => SetPropertyValue(nameof(Name), ref _name, value);
    }

    [Association("Department-Employees")]
    public XPCollection<Employee> Employees => GetCollection<Employee>(nameof(Employees));
}
