using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Validation;

namespace Ferreteria.Module;

public sealed class FerreteriaModule : ModuleBase
{
    public FerreteriaModule()
    {
        RequiredModuleTypes.Add(typeof(ValidationModule));
    }
}
