using System;
using System.Linq;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using PharmacyDemo.Module.BusinessObjects;

namespace PharmacyDemo.Module.Controllers;

/// <summary>
/// Dispensing a prescription: the one operation with real consequences.
/// </summary>
public class DispenseController : ViewController<DetailView>
{
    private readonly SimpleAction _dispenseAction;

    public DispenseController()
    {
        TargetObjectType = typeof(Prescription);

        _dispenseAction = new SimpleAction(this, "DispensePrescription", "Edit")
        {
            Caption = "Dispense",
            ImageName = "Action_Validate",
            ConfirmationMessage = "Dispense this prescription? Stock will be reduced and it cannot be undone.",
            TargetObjectsCriteria = "Not IsDispensed",
            ToolTip = "Reduces stock and marks the prescription dispensed.",
        };

        _dispenseAction.Execute += DispenseAction_Execute;
    }

    private void DispenseAction_Execute(object sender, SimpleActionExecuteEventArgs e)
    {
        var prescription = (Prescription)View.CurrentObject;

        if (prescription.ExpiresOn < DateTime.Today)
        {
            throw new UserFriendlyException("This prescription expired on " + prescription.ExpiresOn.ToShortDateString() + ".");
        }

        foreach (var line in prescription.Lines)
        {
            if (line.Product.OnHand < line.Quantity)
            {
                throw new UserFriendlyException(
                    "Only " + line.Product.OnHand + " units of " + line.Product.Name + " remain.");
            }

            if (HasAllergyConflict(prescription.Patient, line.Product))
            {
                throw new UserFriendlyException(
                    prescription.Patient.FullName + " is recorded as allergic to " + line.Product.Name + ".");
            }
        }

        ReduceStock(prescription);

        prescription.IsDispensed = true;
        ObjectSpace.CommitChanges();
    }

    /// <summary>Takes units from the batches that expire soonest.</summary>
    private static void ReduceStock(Prescription prescription)
    {
        foreach (var line in prescription.Lines)
        {
            var remaining = line.Quantity;

            foreach (var batch in line.Product.Batches.OrderBy(b => b.ExpiresOn))
            {
                if (remaining == 0) break;

                var taken = Math.Min(batch.RemainingUnits, remaining);
                batch.RemainingUnits -= taken;
                remaining -= taken;
            }
        }
    }

    /// <summary>A crude substring check; a real system would use a coded allergen list.</summary>
    private static bool HasAllergyConflict(Patient patient, Product product) =>
        !string.IsNullOrWhiteSpace(patient.Allergies) &&
        patient.Allergies.Contains(product.Name, StringComparison.OrdinalIgnoreCase);
}
