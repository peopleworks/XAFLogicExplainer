using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using Archive.Module.BusinessObjects;

namespace Archive.Module.Controllers.Old;

/// <summary>
/// The controller the application used before <c>InvoiceController</c>. Its folder is removed from
/// the build, so its action is on no screen.
/// </summary>
public class LegacyInvoiceController : ViewController
{
    private readonly SimpleAction reissueInvoice;

    public LegacyInvoiceController()
    {
        TargetObjectType = typeof(Invoice);

        reissueInvoice = new SimpleAction(this, "ReissueInvoice", "Edit")
        {
            Caption = "Reissue invoice"
        };

        reissueInvoice.Execute += ReissueInvoice_Execute;
    }

    private void ReissueInvoice_Execute(object sender, SimpleActionExecuteEventArgs e)
    {
        ObjectSpace.CommitChanges();
    }
}
