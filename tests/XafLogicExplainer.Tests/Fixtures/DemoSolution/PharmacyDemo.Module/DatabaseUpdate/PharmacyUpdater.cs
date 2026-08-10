using System;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Updating;
using PharmacyDemo.Module.BusinessObjects;

namespace PharmacyDemo.Module.DatabaseUpdate;

/// <summary>
/// Creates what a fresh database needs, and migrates data between released versions.
/// </summary>
/// <remarks>
/// The pattern DevExpress recommends, and the one every XAF team ends up with: seeding on first
/// run, and version-gated blocks that run exactly once when an existing database is upgraded.
/// </remarks>
public class PharmacyUpdater : ModuleUpdater
{
    public PharmacyUpdater(IObjectSpace objectSpace, Version currentDBVersion)
        : base(objectSpace, currentDBVersion) { }

    public override void UpdateDatabaseAfterUpdateSchema()
    {
        base.UpdateDatabaseAfterUpdateSchema();

        CreatePaymentMethods();
        CreateDefaultInsurancePlan();

        ObjectSpace.CommitChanges();
    }

    public override void UpdateDatabaseBeforeUpdateSchema()
    {
        base.UpdateDatabaseBeforeUpdateSchema();

        // Runs once, when an existing 1.0 database is upgraded. Prescriptions before this release
        // had no expiry, and a null one would block dispensing on every legacy row.
        if (CurrentDBVersion < new Version("1.1.0.0") && CurrentDBVersion > new Version("0.0.0.0"))
        {
            BackfillPrescriptionExpiry();
        }

        // 1.2 split payment method out of the sale. Existing sales carry no method at all.
        if (CurrentDBVersion < new Version("1.2.0.0") && CurrentDBVersion > new Version("0.0.0.0"))
        {
            AssignCashToLegacyPayments();
        }
    }

    /// <summary>The three ways this pharmacy can be paid.</summary>
    private void CreatePaymentMethods()
    {
        var cash = ObjectSpace.FirstOrDefault<PaymentMethod>(m => m.Name == "Cash");
        if (cash == null)
        {
            cash = ObjectSpace.CreateObject<PaymentMethod>();
            cash.Name = "Cash";
            cash.RequiresAuthorization = false;
        }

        var card = ObjectSpace.FirstOrDefault<PaymentMethod>(m => m.Name == "Card");
        if (card == null)
        {
            card = ObjectSpace.CreateObject<PaymentMethod>();
            card.Name = "Card";
            card.RequiresAuthorization = true;
        }

        var insurer = ObjectSpace.FirstOrDefault<PaymentMethod>(m => m.Name == "Insurer");
        if (insurer == null)
        {
            insurer = ObjectSpace.CreateObject<PaymentMethod>();
            insurer.Name = "Insurer";
            insurer.RequiresAuthorization = true;
        }
    }

    /// <summary>Patients with no insurer still need a plan to point at.</summary>
    private void CreateDefaultInsurancePlan()
    {
        var uninsured = ObjectSpace.FirstOrDefault<InsurancePlan>(p => p.Name == "Uninsured");
        if (uninsured == null)
        {
            uninsured = ObjectSpace.CreateObject<InsurancePlan>();
            uninsured.Name = "Uninsured";
            uninsured.CoveragePercent = 0m;
        }
    }

    /// <summary>Gives legacy prescriptions the 30-day expiry they were always assumed to have.</summary>
    private void BackfillPrescriptionExpiry()
    {
        foreach (var prescription in ObjectSpace.GetObjects<Prescription>())
        {
            if (prescription.ExpiresOn == DateTime.MinValue)
            {
                prescription.ExpiresOn = prescription.IssuedOn.AddDays(30);
            }
        }
    }

    /// <summary>Payments recorded before methods existed were all taken at the counter.</summary>
    private void AssignCashToLegacyPayments()
    {
        var cash = ObjectSpace.FirstOrDefault<PaymentMethod>(m => m.Name == "Cash");

        foreach (var payment in ObjectSpace.GetObjects<Payment>())
        {
            if (payment.Method == null)
            {
                payment.Method = cash;
            }
        }
    }
}
