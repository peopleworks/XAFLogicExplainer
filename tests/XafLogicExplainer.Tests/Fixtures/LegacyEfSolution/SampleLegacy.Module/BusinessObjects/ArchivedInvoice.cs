using DevExpress.Persistent.Base;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SampleLegacy.Module.BusinessObjects;

/// <summary>
/// An invoice that has been closed out. Reachable only through a context that never names
/// <c>DbContext</c> itself.
/// </summary>
[Table("invoice_archive")]
[DefaultClassOptions]
public partial class ArchivedInvoice : BaseEntity
{
    [Key]
    [Column("Id")]
    public virtual int Id { get; set; }

    [Column("ClosedOn")]
    public virtual DateTime ClosedOn { get; set; }
}
