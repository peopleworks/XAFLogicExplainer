using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using Clinica.Module.BusinessObjects;

namespace Clinica.Module.Controllers;

public class FacturaController : ViewController
{
    private readonly SimpleAction aprobar;

    public FacturaController()
    {
        TargetObjectType = typeof(Factura);

        aprobar = new SimpleAction(this, "Aprobar", "Edit")
        {
            Caption = "Aprobar",
            ConfirmationMessage = "Una factura aprobada no se puede modificar. Continuar?",
        };

        aprobar.Execute += Aprobar_Execute;
    }

    private void Aprobar_Execute(object sender, SimpleActionExecuteEventArgs e)
    {
        var factura = (Factura)View.CurrentObject;
        factura.CreadoPor = SecuritySystem.CurrentUserName;
        ObjectSpace.CommitChanges();
    }
}
