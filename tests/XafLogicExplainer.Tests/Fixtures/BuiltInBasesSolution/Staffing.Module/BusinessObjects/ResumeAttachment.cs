using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Staffing.Module.BusinessObjects;

/// <summary>
/// One file attached to a resume, on the library's <c>FileAttachmentBase</c>.
/// </summary>
/// <remarks>
/// It carries no class attribute at all, which is how attachment classes are usually written: they
/// are reached through the collection that owns them, never from navigation. Its base is the only
/// thing that says it is a business class.
/// </remarks>
public class ResumeAttachment : FileAttachmentBase
{
    public ResumeAttachment(Session session) : base(session) { }

    private Resume _resume;

    [Association("Resume-Attachments")]
    public Resume Resume
    {
        get => _resume;
        set => SetPropertyValue(nameof(Resume), ref _resume, value);
    }
}
