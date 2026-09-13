using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using Archive.Module.BusinessObjects;

namespace Archive.Module.Controllers;

/// <summary>
/// Sends an invoice. The controller the application compiles.
/// </summary>
public class InvoiceController : ViewController
{
    private readonly SimpleAction sendInvoice;

    public InvoiceController()
    {
        TargetObjectType = typeof(Invoice);

        sendInvoice = new SimpleAction(this, "SendInvoice", "Edit")
        {
            Caption = "Send invoice"
        };

        sendInvoice.Execute += SendInvoice_Execute;
    }

    private void SendInvoice_Execute(object sender, SimpleActionExecuteEventArgs e)
    {
        ObjectSpace.CommitChanges();
    }
}
