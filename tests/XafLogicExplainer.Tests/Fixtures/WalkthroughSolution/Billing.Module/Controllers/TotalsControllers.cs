using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using Billing.Module.BusinessObjects;

namespace Billing.Module.Controllers;

/// <summary>
/// Declares the recalculate command, and leaves the arithmetic to whoever derives from it.
/// </summary>
/// <remarks>
/// The shape a walk cannot follow from syntax: the handler calls <c>Recalculate()</c>, and which
/// body runs is decided by the run-time type of the controller XAF happened to activate.
/// </remarks>
public class TotalsControllerBase : ViewController<DetailView>
{
    private readonly SimpleAction _recalculateAction;

    public TotalsControllerBase()
    {
        _recalculateAction = new SimpleAction(this, "RecalculateTotals", "Edit")
        {
            Caption = "Recalculate",
        };

        _recalculateAction.Execute += RecalculateAction_Execute;
    }

    private void RecalculateAction_Execute(object sender, SimpleActionExecuteEventArgs e)
    {
        Recalculate();
        ObjectSpace.CommitChanges();
    }

    protected virtual void Recalculate()
    {
    }
}

/// <summary>
/// Recalculates an invoice.
/// </summary>
public class InvoiceTotalsController : TotalsControllerBase
{
    public InvoiceTotalsController()
    {
        TargetObjectType = typeof(Invoice);
    }

    protected override void Recalculate()
    {
        var invoice = (Invoice)View.CurrentObject;

        invoice.Total = LineTotal(invoice);
    }

    private static decimal LineTotal(Invoice invoice) => invoice.Total;
}

/// <summary>
/// Recalculates a credit note.
/// </summary>
public class CreditNoteTotalsController : TotalsControllerBase
{
    public CreditNoteTotalsController()
    {
        TargetObjectType = typeof(CreditNote);
    }

    protected override void Recalculate()
    {
        var note = (CreditNote)View.CurrentObject;

        note.Amount = 0;
    }
}
