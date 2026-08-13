namespace SampleDeep.Module.Contracts;

/// <summary>
/// A wire shape that happens to share a name with the persistent base.
/// </summary>
/// <remarks>
/// Nothing here is persistent. It exists so the walk has to answer *which* NamedBaseObject a
/// class derives from rather than whether some class of that name was accepted.
/// </remarks>
public abstract class NamedBaseObject
{
    public string Name { get; set; }
}
