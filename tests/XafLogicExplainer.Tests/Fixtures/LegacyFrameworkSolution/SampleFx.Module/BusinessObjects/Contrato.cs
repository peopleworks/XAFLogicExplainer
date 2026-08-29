using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace SampleFx.Module.BusinessObjects
{
    /// <summary>
    /// A contract, written the way a .NET Framework XAF project writes one.
    /// </summary>
    /// <remarks>
    /// Property-with-backing-field rather than an auto property, and a block namespace rather than
    /// a file-scoped one, because that is what compiles under the C# version this project gets by
    /// default. The extractor must read it as readily as it reads the modern shape.
    /// </remarks>
    [DefaultClassOptions]
    [NavigationItem("Operaciones")]
    public class Contrato : BaseObject
    {
        private string numero;
        private decimal monto;

        public Contrato(Session session) : base(session) { }

        [Size(20)]
        public string Numero
        {
            get { return numero; }
            set { SetPropertyValue(nameof(Numero), ref numero, value); }
        }

        public decimal Monto
        {
            get { return monto; }
            set { SetPropertyValue(nameof(Monto), ref monto, value); }
        }
    }
}
