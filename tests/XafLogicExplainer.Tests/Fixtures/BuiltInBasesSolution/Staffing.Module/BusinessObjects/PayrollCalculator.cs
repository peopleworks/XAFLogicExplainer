namespace Staffing.Module.BusinessObjects;

/// <summary>
/// A helper that lives among the business classes and is not one: no attribute, no base.
/// </summary>
public class PayrollCalculator
{
    public decimal Net(decimal gross, decimal deductions) => gross - deductions;
}
