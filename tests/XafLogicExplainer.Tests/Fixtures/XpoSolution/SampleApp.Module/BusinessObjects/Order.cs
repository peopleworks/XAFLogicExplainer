using System;
using System.ComponentModel;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace SampleApp.Module.BusinessObjects;

/// <summary>
/// A sales order placed by a customer.
/// </summary>
[DefaultClassOptions]
[NavigationItem("Sales")]
[XafDefaultProperty(nameof(Number))]
[Description("A sales order placed by a customer.")]
[Appearance("OrderLockedWhenApproved", AppearanceItemType = "ViewItem", TargetItems = "*",
    Criteria = "IsApproved", Context = "DetailView", Enabled = false)]
[RuleCriteria("Order_TotalNotNegative", DefaultContexts.Save, "Total >= 0",
    CustomMessageTemplate = "An order total cannot be negative.")]
public class Order : XPCustomObject
{
    public Order(Session session) : base(session) { }

    private string _number;

    [Size(30)]
    [RuleRequiredField("Order_Number_Required", DefaultContexts.Save,
        CustomMessageTemplate = "Every order needs a number.")]
    public string Number
    {
        get => _number;
        set => SetPropertyValue(nameof(Number), ref _number, value);
    }

    private DateTime _orderDate;

    [Description("Date the order was placed.")]
    public DateTime OrderDate
    {
        get => _orderDate;
        set => SetPropertyValue(nameof(OrderDate), ref _orderDate, value);
    }

    private bool _isApproved;

    public bool IsApproved
    {
        get => _isApproved;
        set => SetPropertyValue(nameof(IsApproved), ref _isApproved, value);
    }

    private Customer _customer;

    [Association("Customer-Orders")]
    [DataSourceCriteria("IsBlocked = False")]
    [ImmediatePostData]
    public Customer Customer
    {
        get => _customer;
        set => SetPropertyValue(nameof(Customer), ref _customer, value);
    }

    [Association("Order-Lines"), Aggregated]
    public XPCollection<OrderLine> Lines => GetCollection<OrderLine>(nameof(Lines));

    [PersistentAlias("Lines.Sum(LineTotal)")]
    [Description("Sum of every line on this order.")]
    public decimal Total => Convert.ToDecimal(EvaluateAlias(nameof(Total)));
}
