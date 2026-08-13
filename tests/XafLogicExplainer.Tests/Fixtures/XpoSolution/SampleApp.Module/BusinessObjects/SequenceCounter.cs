using System.ComponentModel;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;

namespace SampleApp.Module.BusinessObjects;

/// <summary>
/// The next number to hand out for a document type. Derived from <c>PersistentBase</c>, the root
/// of the XPO hierarchy and the class DevExpress recommends as a base for persistent classes.
/// </summary>
[DefaultClassOptions]
public class SequenceCounter : PersistentBase
{
    public SequenceCounter(Session session) : base(session) { }

    [Key, Size(60)]
    public string TypeName { get; set; }

    public long NextNumber { get; set; }
}
