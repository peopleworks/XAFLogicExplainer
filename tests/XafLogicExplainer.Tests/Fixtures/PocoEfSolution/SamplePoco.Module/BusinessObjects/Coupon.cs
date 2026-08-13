using DevExpress.Persistent.Base;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SamplePoco.Module.BusinessObjects;

[Table("coupon")]
[DefaultClassOptions]
[NavigationItem("Promotions")]
public class Coupon
{
    [Key]
    [Column("Code")]
    [StringLength(12)]
    public virtual string Code { get; set; }

    [Column("Percentage")]
    public virtual decimal Percentage { get; set; }
}
