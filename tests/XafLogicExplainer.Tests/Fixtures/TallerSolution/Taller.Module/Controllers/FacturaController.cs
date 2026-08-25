using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using Taller.Module.BusinessObjects;

namespace Taller.Module.Controllers;

public class FacturaController : ViewController
{
    private readonly SimpleAction aprobar;

    public FacturaController()
    {
        TargetObjectType = typeof(Factura);

        aprobar = new SimpleAction(this, "Aprobar", "Edit") { Caption = "Aprobar" };
        aprobar.Execute += Aprobar_Execute;
    }

    private void Aprobar_Execute(object sender, SimpleActionExecuteEventArgs e) =>
        ObjectSpace.CommitChanges();
}
