using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SampleLegacy.Module.BusinessObjects;

/// <summary>
/// The hand-written half of a scaffolded class: the attributes and the XAF base, kept where a
/// regeneration will not overwrite them.
/// </summary>
/// <remarks>
/// The other half lives in <c>Shipment.Generated.cs</c>. Splitting a class this way is the norm
/// wherever a tool writes the column mapping, and it is what makes a name-matched roster report
/// the same entity once per file.
/// </remarks>
[Table("shipment")]
[DefaultClassOptions]
[NavigationItem("Sales")]
[XafDisplayName("Shipments")]
public partial class Shipment : BaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column("Id")]
    public virtual int Id { get; set; }

    [Column("Tracking")]
    [StringLength(32)]
    public virtual string? TrackingNumber { get; set; }
}
