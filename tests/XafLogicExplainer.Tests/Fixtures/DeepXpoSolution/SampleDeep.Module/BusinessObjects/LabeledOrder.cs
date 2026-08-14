using DevExpress.Persistent.Base;
using DevExpress.Xpo;

namespace SampleDeep.Module.BusinessObjects;

/// <summary>Redeclares a property its base already declares.</summary>
/// <remarks>
/// Here so folding has to answer what a redeclaration means: the class's own <c>Number</c> is the
/// property, and the inherited one must not appear beside it as a duplicate row.
/// </remarks>
[DefaultClassOptions]
public class LabeledOrder : Order
{
    public LabeledOrder(Session session) : base(session) { }

    public new string Number
    {
        get => GetPropertyValue<string>(nameof(Number));
        set => SetPropertyValue(nameof(Number), value);
    }
}
