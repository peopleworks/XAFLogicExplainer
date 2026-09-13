using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Staffing.Module.BusinessObjects;

/// <summary>
/// A candidate's resume, holding its attachments.
/// </summary>
[DefaultClassOptions]
public class Resume : BaseObject
{
    public Resume(Session session) : base(session) { }

    private string _candidate;

    public string Candidate
    {
        get => _candidate;
        set => SetPropertyValue(nameof(Candidate), ref _candidate, value);
    }

    [Aggregated, Association("Resume-Attachments")]
    public XPCollection<ResumeAttachment> Attachments => GetCollection<ResumeAttachment>(nameof(Attachments));
}
