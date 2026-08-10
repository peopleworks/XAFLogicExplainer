using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl.EF;

namespace SampleEf.Module.BusinessObjects;

/// <summary>
/// Something the company sells. Persisted with EF Core.
/// </summary>
[DefaultClassOptions]
[NavigationItem("Catalogue")]
[XafDefaultProperty(nameof(Title))]
[Description("Something the company sells.")]
public class Product : BaseObject
{
    [StringLength(150)]
    [Required]
    [Description("Name shown to customers.")]
    public virtual string Title { get; set; }

    [StringLength(40)]
    public virtual string Sku { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public virtual decimal Price { get; set; }

    public virtual bool IsDiscontinued { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public virtual Category Category { get; set; }

    public virtual int? CategoryId { get; set; }

    [NotMapped]
    [Description("Not stored; derived for display only.")]
    public virtual string DisplayLabel => $"{Sku} — {Title}";
}
