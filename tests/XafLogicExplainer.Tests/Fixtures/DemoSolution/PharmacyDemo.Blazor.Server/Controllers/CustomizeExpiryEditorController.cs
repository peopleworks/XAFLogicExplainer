using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Blazor.Editors;

namespace PharmacyDemo.Blazor.Server.Controllers;

/// <summary>
/// Shows the time alongside the date on every expiry field.
/// </summary>
/// <remarks>
/// The other way a screen stops matching its business class: no custom editor exists, a controller
/// simply reaches into a built-in one at run time. Nothing on the entity or in the Model Editor
/// records that this happens.
/// </remarks>
public class CustomizeExpiryEditorController : ViewController<DetailView>
{
    protected override void OnActivated()
    {
        base.OnActivated();

        View.CustomizeViewItemControl<DateTimePropertyEditor>(this, editor =>
        {
            editor.ComponentModel.TimeSectionVisible = true;
        });
    }
}
