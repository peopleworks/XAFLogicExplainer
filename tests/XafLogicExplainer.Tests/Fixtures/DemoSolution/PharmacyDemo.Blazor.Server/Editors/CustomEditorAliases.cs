namespace PharmacyDemo.Blazor.Server.Editors;

/// <summary>
/// The aliases a property can ask for with <c>[EditorAlias]</c>.
/// </summary>
/// <remarks>
/// Gathered in one place, which is what most teams do — and the reason a registration attribute
/// reads as a constant reference rather than as the string XAF actually matches on.
/// </remarks>
public struct CustomEditorAliases
{
    public const string BarcodeScannerPropertyEditor = "BarcodeScannerPropertyEditor";
    public const string ExpiryCalendarListEditor = "ExpiryCalendarListEditor";
}
