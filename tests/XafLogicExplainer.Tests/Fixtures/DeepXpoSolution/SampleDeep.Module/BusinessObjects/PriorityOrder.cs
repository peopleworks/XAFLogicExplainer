using DevExpress.Persistent.Base;
using DevExpress.Xpo;

namespace SampleDeep.Module.BusinessObjects;

/// <summary>Three hops: PriorityOrder -> Order -> NamedBaseObject -> BaseObject.</summary>
/// <remarks>
/// Here so the walk is a fixed point rather than one extra hop. It only resolves once
/// <see cref="Order"/> has been accepted, which may be in a later round.
/// </remarks>
[DefaultClassOptions]
public class PriorityOrder : Order
{
    public PriorityOrder(Session session) : base(session) { }

    public int Rank
    {
        get => GetPropertyValue<int>(nameof(Rank));
        set => SetPropertyValue(nameof(Rank), value);
    }
}
