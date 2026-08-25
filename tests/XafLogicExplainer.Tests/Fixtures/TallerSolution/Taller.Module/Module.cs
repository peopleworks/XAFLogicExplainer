using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Validation;

namespace Taller.Module;

public sealed class TallerModule : ModuleBase
{
    public TallerModule()
    {
        RequiredModuleTypes.Add(typeof(ValidationModule));
    }
}
