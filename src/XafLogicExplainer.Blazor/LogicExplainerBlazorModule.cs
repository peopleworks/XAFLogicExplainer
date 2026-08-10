using DevExpress.ExpressApp;

namespace XafLogicExplainer.Blazor;

/// <summary>
/// XAF Module that registers the Copilot widget components.
/// Add this module to your XAF application to enable the Copilot help panel.
///
/// In your Module.cs:
/// <code>
/// RequiredModuleTypes.Add(typeof(LogicExplainerBlazorModule));
/// </code>
/// </summary>
public sealed class LogicExplainerBlazorModule : ModuleBase
{
    protected override IEnumerable<Type> GetDeclaredControllerTypes()
    {
        return
        [
            typeof(Controllers.CopilotContextController)
        ];
    }
}
