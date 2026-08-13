namespace SampleDeep.Module.Contracts;

/// <summary>
/// The DTO an integration posts. Same simple name as the entity, same base name as the entity's
/// base, and persistent in neither sense.
/// </summary>
/// <remarks>
/// There is no using directive for the BusinessObjects namespace here, so
/// <c>NamedBaseObject</c> resolves to the one beside it. A walk that asked only "was a class
/// called NamedBaseObject accepted" would answer yes and turn this into a table.
/// </remarks>
public class Order : NamedBaseObject
{
    public string Number { get; set; }
}
