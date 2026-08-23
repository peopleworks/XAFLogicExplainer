using System;
using System.Collections.Generic;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ReportsV2;
using DevExpress.ExpressApp.Updating;
using Invoicing.Module.BusinessObjects;
using Invoicing.Module.DatabaseUpdate;
using Invoicing.Module.Reports;

namespace Invoicing.Module;

/// <summary>
/// The XAF module. Registers the predefined reports through every overload
/// <c>PredefinedReportsUpdater.AddPredefinedReport</c> has.
/// </summary>
public sealed class InvoicingModule : ModuleBase
{
    public InvoicingModule()
    {
        RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.SystemModule.SystemModule));
        RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.ReportsV2.ReportsModuleV2));

        AdditionalExportedTypes.Add(typeof(Customer));
        AdditionalExportedTypes.Add(typeof(Invoice));
    }

    public override IEnumerable<ModuleUpdater> GetModuleUpdaters(IObjectSpace objectSpace, Version versionFromDB)
    {
        ModuleUpdater updater = new Updater(objectSpace, versionFromDB);

        var reports = new PredefinedReportsUpdater(Application, objectSpace, versionFromDB);

        // (displayName, dataType): nothing said about in-place.
        reports.AddPredefinedReport<InvoiceReport>("Invoice", typeof(Invoice));

        // (displayName, dataType, isInplaceReport)
        reports.AddPredefinedReport<OverdueInvoicesReport>("Overdue invoices", typeof(Invoice), true);

        // (displayName, dataType, parametersObjectType)
        reports.AddPredefinedReport<CustomerStatementReport>("Customer statement", typeof(Customer), typeof(StatementParameters));

        // (displayName, dataType, parametersObjectType, isInplaceReport)
        reports.AddPredefinedReport<CustomerStatementReport>("Customer statement (in place)", typeof(Customer), typeof(StatementParameters), false);

        return new ModuleUpdater[] { updater, reports };
    }
}
