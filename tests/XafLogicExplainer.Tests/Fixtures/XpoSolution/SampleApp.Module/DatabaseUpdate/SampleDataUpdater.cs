using System;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Updating;
using SampleApp.Module.BusinessObjects;

namespace SampleApp.Module.DatabaseUpdate;

/// <summary>
/// Creates the records the application needs on a fresh database.
/// </summary>
public class SampleDataUpdater : ModuleUpdater
{
    public SampleDataUpdater(IObjectSpace objectSpace, Version currentDBVersion)
        : base(objectSpace, currentDBVersion) { }

    public override void UpdateDatabaseAfterUpdateSchema()
    {
        base.UpdateDatabaseAfterUpdateSchema();
        CreateDefaultCustomers();
        ObjectSpace.CommitChanges();
    }

    /// <summary>
    /// Seeds the two customers the demo data depends on.
    /// </summary>
    private void CreateDefaultCustomers()
    {
        var walkIn = ObjectSpace.FirstOrDefault<Customer>(c => c.Name == "Walk-in");
        if (walkIn == null)
        {
            walkIn = ObjectSpace.CreateObject<Customer>();
            walkIn.Name = "Walk-in";
            walkIn.TaxId = "000000000";
            walkIn.IsBlocked = false;
        }

        var wholesale = ObjectSpace.FirstOrDefault<Customer>(c => c.Name == "Wholesale");
        if (wholesale == null)
        {
            wholesale = ObjectSpace.CreateObject<Customer>();
            wholesale.Name = "Wholesale";
            wholesale.TaxId = "111111111";
        }
    }
}
