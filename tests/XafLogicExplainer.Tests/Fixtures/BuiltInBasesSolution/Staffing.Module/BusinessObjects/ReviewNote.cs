namespace Staffing.Module.BusinessObjects;

/// <summary>
/// A note left on a performance review. It derives from this application's own <see cref="Note"/>.
/// </summary>
public class ReviewNote : Note
{
    public string Reviewer { get; set; }
}
