using DevExpress.Persistent.BaseImpl;

namespace Homonym.Core;

/// <summary>
/// The library's own Cliente. A module that declares its own under this namespace shadows it.
/// </summary>
public class Cliente : BaseObject
{
    public string CodigoHeredado { get; set; }
}
