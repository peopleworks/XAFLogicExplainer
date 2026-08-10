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

    [Size(160)]
    [RuleRequiredField("Product_Name", DefaultContexts.Save, CustomMessageTemplate = "A product must have a name.")]
    private string _name;
    public string Name { get => _name; set => SetPropertyValue(nameof(Name), ref _name, value); }

    [Size(32)]
    [Indexed(Unique = true)]
    private string _barcode;
    public string Barcode { get => _barcode; set => SetPropertyValue(nameof(Barcode), ref _barcode, value); }

    private decimal _unitPrice;
    public decimal UnitPrice { get => _unitPrice; set => SetPropertyValue(nameof(UnitPrice), ref _unitPrice, value); }

    [Description("Cannot be dispensed without a prescription on file.")]
    private bool _requiresPrescription;
    public bool RequiresPrescription { get => _requiresPrescription; set => SetPropertyValue(nameof(RequiresPrescription), ref _requiresPrescription, value); }

    [Association("Category-Products")]
    private ProductCategory _category;
    public ProductCategory Category { get => _category; set => SetPropertyValue(nameof(Category), ref _category, value); }

    [Association("Supplier-Products")]
    [DataSourceCriteria("IsPreferred = True")]
    private Supplier _supplier;
    public Supplier Supplier { get => _supplier; set => SetPropertyValue(nameof(Supplier), ref _supplier, value); }

    [Association("Product-Batches"), Aggregated]
    public XPCollection<StockBatch> Batches => GetCollection<StockBatch>(nameof(Batches));

    [PersistentAlias("Batches.Sum(RemainingUnits)")]
    [Description("Units across every unexpired batch.")]
    public int OnHand => (int)EvaluateAlias(nameof(OnHand));
}
