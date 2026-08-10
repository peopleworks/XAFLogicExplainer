using System;
using System.Collections.Generic;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Updating;
using SampleApp.Module.BusinessObjects;
using SampleApp.Module.DatabaseUpdate;

namespace SampleApp.Module;

/// <summary>
/// The XAF module. Registers business classes and declares module dependencies.
/// </summary>
public sealed class SampleAppModule : ModuleBase
{
    public SampleAppModule()
    {
        // Genuine module dependencies.
        RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.SystemModule.SystemModule));
        RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.Validation.ValidationModule));
        RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.ConditionalAppearance.ConditionalAppearanceModule));

        // Business classes, added to a DIFFERENT collection in the same constructor.
        // These must never be reported as required modules.
        AdditionalExportedTypes.Add(typeof(Customer));
        AdditionalExportedTypes.Add(typeof(Order));
        AdditionalExportedTypes.Add(typeof(OrderLine));
    }

    public override IEnumerable<ModuleUpdater> GetModuleUpdaters(IObjectSpace objectSpace, Version versionFromDB)
    {
        yield return new SampleDataUpdater(objectSpace, versionFromDB);
    }
}
