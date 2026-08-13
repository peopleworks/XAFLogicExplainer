namespace SampleLegacy.Module.BusinessObjects.Contracts;

/// <summary>
/// The shape a warehouse takes on the wire. Nothing persists it, and the DbContext -- one
/// namespace out, importing nothing from here -- could not be naming it: C# resolves an
/// unqualified name outwards, never down into a child namespace.
/// </summary>
/// <remarks>
/// It exists to share a simple name with a real entity. A roster of bare names cannot tell the two
/// apart, and would tell an agent this is a table.
/// </remarks>
public class Warehouse
{
    public string Code { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;
}
