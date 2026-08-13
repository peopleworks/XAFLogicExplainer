using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SampleLegacy.Module.BusinessObjects;

/// <summary>
/// A bill mapped onto a table that already existed. Registered as a DbSet, not a BaseObject.
/// </summary>
[Table("invoice")]
[DefaultClassOptions]
[NavigationItem("Sales")]
[XafDisplayName("Invoices")]
[XafDefaultProperty(nameof(Number))]
[Appearance("InvoicePaid", TargetItems = "*", Criteria = "IsPaid", BackColor = "LightGreen")]
public partial class Invoice : BaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column("Id")]
    public virtual int Id { get; set; }

    [Column("Number")]
    [StringLength(20)]
    [RuleRequiredField("Invoice_Number_Required", DefaultContexts.Save)]
    public virtual string Number { get; set; }

    [Column("Amount")]
    public virtual decimal Amount { get; set; }

    [Column("IsPaid")]
    public virtual bool IsPaid { get; set; }
}
