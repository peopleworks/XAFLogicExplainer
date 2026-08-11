using System;
using System.ComponentModel;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace PharmacyDemo.Module.BusinessObjects;

/// <summary>Something the pharmacy sells.</summary>
[DefaultClassOptions]
[NavigationItem("Catalogue")]
[XafDefaultProperty(nameof(Name))]
[Description("Something the pharmacy sells.")]
[Appearance("ProductOutOfStock", Criteria = "OnHand = 0", TargetItems = "*", Context = "ListView", FontColor = "Red")]
public class Product : XPCustomObject
{
    public Product(Session session) : base(session) { }

    private string _name;
    [Size(160)]
    [RuleRequiredField("Product_Name", DefaultContexts.Save, CustomMessageTemplate = "A product must have a name.")]
    public string Name { get => _name; set => SetPropertyValue(nameof(Name), ref _name, value); }

    private string _barcode;
    [Size(32)]
    [Indexed(Unique = true)]
    public string Barcode { get => _barcode; set => SetPropertyValue(nameof(Barcode), ref _barcode, value); }

    private decimal _unitPrice;
    public decimal UnitPrice { get => _unitPrice; set => SetPropertyValue(nameof(UnitPrice), ref _unitPrice, value); }

    private bool _requiresPrescription;
    [Description("Cannot be dispensed without a prescription on file.")]
    public bool RequiresPrescription { get => _requiresPrescription; set => SetPropertyValue(nameof(RequiresPrescription), ref _requiresPrescription, value); }

    private ProductCategory _category;
    [Association("Category-Products")]
    public ProductCategory Category { get => _category; set => SetPropertyValue(nameof(Category), ref _category, value); }

    private Supplier _supplier;
    [Association("Supplier-Products")]
    [DataSourceCriteria("IsPreferred = True")]
    public Supplier Supplier { get => _supplier; set => SetPropertyValue(nameof(Supplier), ref _supplier, value); }

    [Association("Product-Batches"), Aggregated]
    public XPCollection<StockBatch> Batches => GetCollection<StockBatch>(nameof(Batches));

    [PersistentAlias("Batches.Sum(RemainingUnits)")]
    [Description("Units across every batch, expired ones included.")]
    public int OnHand => (int)EvaluateAlias(nameof(OnHand));
}
