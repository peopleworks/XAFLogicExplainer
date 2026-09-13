using System.ComponentModel;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;

namespace Archive.Module.BusinessObjects.Drafts;

/// <summary>
/// A statement screen somebody started and set aside. It carries every attribute that would put it in
/// navigation, and its whole folder is removed from the build.
/// </summary>
[DomainComponent]
[DefaultClassOptions]
[NavigationItem("Billing")]
public class DraftStatement : IXafEntityObject, INotifyPropertyChanged
{
    public string Period { get; set; }

    public decimal Balance { get; set; }

    public event PropertyChangedEventHandler PropertyChanged;

    public void OnCreated() { }

    public void OnLoaded() { }

    public void OnSaving() { }
}
