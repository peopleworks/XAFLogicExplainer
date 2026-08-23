using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Updating;
using Invoicing.Module.BusinessObjects;

namespace Invoicing.Module.DatabaseUpdate;

public class Updater : ModuleUpdater
{
    public Updater(IObjectSpace objectSpace, Version currentDBVersion) : base(objectSpace, currentDBVersion) { }

    public override void UpdateDatabaseAfterUpdateSchema()
    {
        base.UpdateDatabaseAfterUpdateSchema();
        CreateDemoCustomer();
    }

    private void CreateDemoCustomer()
    {
        var customer = ObjectSpace.FirstOrDefault<Customer>(c => c.Name == "Acme");
        if (customer == null)
        {
            customer = ObjectSpace.CreateObject<Customer>();
            customer.Name = "Acme";
            customer.Region = "North";
        }
        ObjectSpace.CommitChanges();
    }
}
