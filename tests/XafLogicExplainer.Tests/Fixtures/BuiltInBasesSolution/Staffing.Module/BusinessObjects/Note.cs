namespace Staffing.Module.BusinessObjects;

/// <summary>
/// A plain note, named like the library's <c>Note</c> class and unrelated to it.
/// </summary>
/// <remarks>
/// C# binds the name to this declaration, not to the library's, so a class deriving from it derives
/// from a plain object. A library base recognised by name has to lose to a class the application
/// declares itself.
/// </remarks>
public class Note
{
    public string Text { get; set; }
}
