using System;
using System.Collections.Generic;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Updating;
using PharmacyDemo.Module.BusinessObjects;
using PharmacyDemo.Module.DatabaseUpdate;

namespace PharmacyDemo.Module;

/// <summary>
/// The XAF module for the dispensing pharmacy demo.
/// </summary>
public sealed class PharmacyDemoModule : ModuleBase
{
    public PharmacyDemoModule()
    {
        RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.SystemModule.SystemModule));
        RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.Validation.ValidationModule));
        RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.ConditionalAppearance.ConditionalAppearanceModule));

        AdditionalExportedTypes.Add(typeof(Product));
        AdditionalExportedTypes.Add(typeof(Sale));
        AdditionalExportedTypes.Add(typeof(Prescription));
        AdditionalExportedTypes.Add(typeof(Patient));
    }

    public override IEnumerable<ModuleUpdater> GetModuleUpdaters(IObjectSpace objectSpace, Version versionFromDB)
    {
        yield return new PharmacyUpdater(objectSpace, versionFromDB);
    }
}
