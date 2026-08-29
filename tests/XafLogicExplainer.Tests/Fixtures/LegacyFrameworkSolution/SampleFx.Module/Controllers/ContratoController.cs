using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using SampleFx.Module.BusinessObjects;

namespace SampleFx.Module.Controllers
{
    public class ContratoController : ViewController
    {
        private SimpleAction cerrarContrato;

        public ContratoController()
        {
            TargetObjectType = typeof(Contrato);

            cerrarContrato = new SimpleAction(this, "CerrarContrato", "Edit")
            {
                Caption = "Cerrar contrato"
            };

            cerrarContrato.Execute += CerrarContrato_Execute;
        }

        private void CerrarContrato_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            ObjectSpace.CommitChanges();
        }
    }
}
