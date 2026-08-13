using DevExpress.Persistent.Base;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SampleLegacy.Module.BusinessObjects;

/// <summary>
/// A stock location. No XAF base class and no XAF interfaces at all — a plain mapped POCO,
/// which is what a scaffolded legacy table usually produces.
/// </summary>
[Table("warehouse")]
[DefaultClassOptions]
public partial class Warehouse
{
    [Key]
    [Column("Code")]
    [StringLength(10)]
    public virtual string Code { get; set; }

    [Column("City")]
    [StringLength(60)]
    public virtual string City { get; set; }
}
