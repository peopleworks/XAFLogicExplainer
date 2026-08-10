using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl.EF;

namespace SampleEf.Module.BusinessObjects;

/// <summary>
/// Groups products in the catalogue.
/// </summary>
[DefaultClassOptions]
[NavigationItem("Catalogue")]
[XafDefaultProperty(nameof(Name))]
public class Category : BaseObject
{
    [StringLength(80)]
    [Required]
    public virtual string Name { get; set; }

    [MaxLength(400)]
    public virtual string Notes { get; set; }

    public virtual IList<Product> Products { get; set; } = new List<Product>();
}
