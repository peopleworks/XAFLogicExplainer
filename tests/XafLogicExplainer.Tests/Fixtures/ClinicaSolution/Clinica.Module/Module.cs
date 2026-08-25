using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Validation;

namespace Clinica.Module;

public sealed class ClinicaModule : ModuleBase
{
    public ClinicaModule()
    {
        RequiredModuleTypes.Add(typeof(ValidationModule));
        RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.Security.SecurityModule));
    }
}
