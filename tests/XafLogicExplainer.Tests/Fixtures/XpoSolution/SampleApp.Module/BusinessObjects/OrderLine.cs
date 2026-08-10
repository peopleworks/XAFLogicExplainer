using System;
using System.ComponentModel;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;

namespace SampleApp.Module.BusinessObjects;

/// <summary>
/// One product line on an order.
/// </summary>
[Description("One product line on an order.")]
public class OrderLine : XPCustomObject
{
    public OrderLine(Session session) : base(session) { }

    private Order _order;

    [Association("Order-Lines")]
    public Order Order
    {
        get => _order;
        set => SetPropertyValue(nameof(Order), ref _order, value);
    }

    private string _productName;

    [Size(200)]
    public string ProductName
    {
        get => _productName;
        set => SetPropertyValue(nameof(ProductName), ref _productName, value);
    }

    private int _quantity;

    public int Quantity
    {
        get => _quantity;
        set => SetPropertyValue(nameof(Quantity), ref _quantity, value);
    }

    private decimal _unitPrice;

    public decimal UnitPrice
    {
        get => _unitPrice;
        set => SetPropertyValue(nameof(UnitPrice), ref _unitPrice, value);
    }

    [PersistentAlias("Quantity * UnitPrice")]
    public decimal LineTotal => Convert.ToDecimal(EvaluateAlias(nameof(LineTotal)));
}
