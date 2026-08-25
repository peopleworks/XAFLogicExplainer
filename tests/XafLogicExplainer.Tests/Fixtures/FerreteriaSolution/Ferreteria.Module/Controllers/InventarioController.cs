using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using Ferreteria.Module.BusinessObjects;

namespace Ferreteria.Module.Controllers;

public class InventarioController : ViewController
{
    private readonly SimpleAction recontar;

    public InventarioController()
    {
        TargetObjectType = typeof(Producto);

        recontar = new SimpleAction(this, "Recontar", "Tools") { Caption = "Recontar existencia" };
        recontar.Execute += Recontar_Execute;
    }

    private void Recontar_Execute(object sender, SimpleActionExecuteEventArgs e) =>
        ObjectSpace.CommitChanges();
}
