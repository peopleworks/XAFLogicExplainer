using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Staffing.Module.BusinessObjects;

/// <summary>
/// An employee, on the <c>Person</c> class DevExpress shipped in its business class library.
/// </summary>
/// <remarks>
/// The shape that went missing. Its base is not one of the root persistent classes and is not
/// declared in this application, so nothing connected it to one — and the class every staffing
/// screen is built on was absent from every output, while both ends of its associations were in the
/// source.
/// </remarks>
[DefaultClassOptions]
[NavigationItem("Staff")]
public class Employee : Person
{
    public Employee(Session session) : base(session) { }

    private Department _department;

    [Association("Department-Employees")]
    public Department Department
    {
        get => _department;
        set => SetPropertyValue(nameof(Department), ref _department, value);
    }

    private decimal _salary;

    public decimal Salary
    {
        get => _salary;
        set => SetPropertyValue(nameof(Salary), ref _salary, value);
    }
}
