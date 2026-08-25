using DevExpress.Persistent.Base;
using DevExpress.Xpo;

namespace Clinica.Module.BusinessObjects;

[DefaultClassOptions]
public class Consulta : AuditedEntity
{
    public Consulta(Session session) : base(session) { }

    public Cliente Paciente { get; set; }

    [Size(400)]
    public string Diagnostico { get; set; }
}
