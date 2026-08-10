using DevExpress.ExpressApp.Blazor.Editors;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Model;
using Microsoft.JSInterop;

namespace PharmacyDemo.Blazor.Server.Editors;

/// <summary>
/// Reads a barcode from the counter scanner instead of showing a text box.
/// </summary>
public class BarcodeScannerModel : ComponentModelBase
{
    public string Value
    {
        get => GetPropertyValue<string>();
        set => SetPropertyValue(value);
    }
}

/// <summary>
/// Replaces the text box on barcode properties with a live scanner. Depends on the browser
/// camera API, which is why it carries its own script.
/// </summary>
[PropertyEditor(typeof(string), CustomEditorAliases.BarcodeScannerPropertyEditor, false)]
public class BarcodeScannerPropertyEditor : BlazorPropertyEditorBase
{
    private readonly IJSRuntime _js;

    public BarcodeScannerPropertyEditor(Type objectType, IModelMemberViewItem model, IJSRuntime js)
        : base(objectType, model)
    {
        _js = js;
    }

    protected override IComponentModel CreateComponentModel() => new BarcodeScannerModel();

    protected override void ReadValueCore()
    {
        base.ReadValueCore();
        _js.InvokeVoidAsync("import", "./js/barcode-scanner.js");
    }
}
