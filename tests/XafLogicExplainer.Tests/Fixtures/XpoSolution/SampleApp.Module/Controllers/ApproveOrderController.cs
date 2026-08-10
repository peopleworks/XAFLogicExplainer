using System;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using SampleApp.Module.BusinessObjects;

namespace SampleApp.Module.Controllers;

/// <summary>
/// Adds the approval command to an order's detail view.
/// </summary>
public class ApproveOrderController : ViewController<DetailView>
{
    private readonly SimpleAction _approveAction;

    public ApproveOrderController()
    {
        TargetObjectType = typeof(Order);

        _approveAction = new SimpleAction(this, "ApproveOrder", "Edit")
        {
            Caption = "Approve",
            ImageName = "State_Validation_Valid",
            ConfirmationMessage = "Approve this order? It cannot be edited afterwards.",
            TargetObjectsCriteria = "Not IsApproved",
            ToolTip = "Marks the order approved and locks it for editing.",
        };

        _approveAction.Execute += ApproveAction_Execute;
    }

    private void ApproveAction_Execute(object sender, SimpleActionExecuteEventArgs e)
    {
        var order = (Order)View.CurrentObject;

        if (order.Customer is { IsBlocked: true })
        {
            throw new UserFriendlyException("A blocked customer cannot have orders approved.");
        }

        if (order.Total <= 0)
        {
            throw new UserFriendlyException("An order with no value cannot be approved.");
        }

        order.IsApproved = true;
        ObjectSpace.CommitChanges();
    }

    /// <summary>
    /// Whether the order is in a state that allows approval.
    /// </summary>
    private static bool CanApprove(Order order) =>
        order is { IsApproved: false, Total: > 0 };
}
