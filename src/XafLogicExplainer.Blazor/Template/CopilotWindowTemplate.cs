using Microsoft.AspNetCore.Components;

namespace XafLogicExplainer.Blazor.Template;

/// <summary>
/// XAF Blazor window template that includes the PeopleWorks Copilot panel.
/// Inherits from ApplicationWindowTemplate to get standard XAF behavior.
///
/// Usage in BlazorApplication.cs:
/// <code>
/// protected override IFrameTemplate CreateDefaultTemplate(TemplateContext context)
/// {
///     if (context == TemplateContext.ApplicationWindow)
///         return new CopilotWindowTemplate();
///     return base.CreateDefaultTemplate(context);
/// }
/// </code>
/// </summary>
public class CopilotWindowTemplate : DevExpress.ExpressApp.Blazor.Templates.ApplicationWindowTemplate
{
    protected override RenderFragment CreateComponent()
    {
        return CopilotWindowTemplateComponent.Create(this);
    }
}
