using System.ComponentModel.DataAnnotations.Schema;

namespace SampleLegacy.Module.BusinessObjects;

// Generated from the database schema. Do not edit -- regenerated on every scaffold.
public partial class Shipment
{
    [Column("DispatchedOn")]
    public virtual DateTime? DispatchedOn { get; set; }

    [Column("IsDelivered")]
    public virtual bool IsDelivered { get; set; }
}
