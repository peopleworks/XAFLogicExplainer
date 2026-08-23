using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Diff;

/// <summary>
/// Compares two extracted project snapshots and builds a structured diff report.
/// </summary>
public class ProjectDiffEngine
{
    /// <summary>
    /// Produces a diff report between two extraction snapshots.
    /// </summary>
    /// <param name="previous">Previous extracted snapshot.</param>
    /// <param name="current">Current extracted snapshot.</param>
    /// <returns>Diff report containing summary and per-domain details.</returns>
    public ProjectDiffReport Compare(ExtractedProject previous, ExtractedProject current)
    {
        var report = new ProjectDiffReport
        {
            ProjectName = current.ProjectName,
            PreviousHash = previous.SourceHash,
            CurrentHash = current.SourceHash,
            PreviousExtractedAt = previous.ExtractedAt,
            CurrentExtractedAt = current.ExtractedAt
        };

        DiffEntities(previous, current, report);
        DiffControllers(previous, current, report);
        DiffNavigation(previous, current, report);
        DiffSeedData(previous, current, report);
        DiffModelEditor(previous, current, report);

        // Compute summary totals
        report.Summary.TotalAdded = report.Summary.EntitiesAdded + report.Summary.ControllersAdded +
                                    report.Summary.NavigationAdded + report.Summary.SeedDataAdded;
        report.Summary.TotalRemoved = report.Summary.EntitiesRemoved + report.Summary.ControllersRemoved +
                                      report.Summary.NavigationRemoved + report.Summary.SeedDataRemoved;
        report.Summary.TotalModified = report.Summary.EntitiesModified + report.Summary.ControllersModified +
                                       report.Summary.NavigationModified;

        return report;
    }

    /// <summary>
    /// Compares entity collections and updates entity summary counters.
    /// </summary>
    private void DiffEntities(ExtractedProject prev, ExtractedProject curr, ProjectDiffReport report)
    {
        var prevMap = ToSafeDictionary(prev.Entities, e => e.ClassName);
        var currMap = ToSafeDictionary(curr.Entities, e => e.ClassName);

        // Added entities
        foreach (var name in currMap.Keys.Except(prevMap.Keys))
            report.EntityChanges.Added.Add(name);

        // Removed entities
        foreach (var name in prevMap.Keys.Except(currMap.Keys))
            report.EntityChanges.Removed.Add(name);

        // Modified entities
        foreach (var name in prevMap.Keys.Intersect(currMap.Keys))
        {
            var diff = DiffEntity(prevMap[name], currMap[name]);
            if (diff.HasChanges)
                report.EntityChanges.Modified.Add(diff);
        }

        report.Summary.EntitiesAdded = report.EntityChanges.Added.Count;
        report.Summary.EntitiesRemoved = report.EntityChanges.Removed.Count;
        report.Summary.EntitiesModified = report.EntityChanges.Modified.Count;
    }

    /// <summary>
    /// Produces a detailed diff for one entity.
    /// </summary>
    private EntityDiff DiffEntity(ExtractedEntity prev, ExtractedEntity curr)
    {
        var diff = new EntityDiff { ClassName = curr.ClassName };

        // Description change
        if (prev.Description != curr.Description)
            diff.DescriptionChange = new FieldChange { FieldName = "Description", OldValue = prev.Description, NewValue = curr.Description };

        // Caption change (from xafml)
        if (prev.ModelCaption != curr.ModelCaption)
            diff.CaptionChange = new FieldChange { FieldName = "ModelCaption", OldValue = prev.ModelCaption, NewValue = curr.ModelCaption };

        // BaseType change
        if (prev.BaseType != curr.BaseType)
            diff.BaseTypeChange = new FieldChange { FieldName = "BaseType", OldValue = prev.BaseType, NewValue = curr.BaseType };

        // Properties
        var prevProps = ToSafeDictionary(prev.Properties, p => p.Name);
        var currProps = ToSafeDictionary(curr.Properties, p => p.Name);

        foreach (var name in currProps.Keys.Except(prevProps.Keys))
            diff.AddedProperties.Add($"{name} ({currProps[name].TypeName})");

        foreach (var name in prevProps.Keys.Except(currProps.Keys))
            diff.RemovedProperties.Add($"{name} ({prevProps[name].TypeName})");

        foreach (var name in prevProps.Keys.Intersect(currProps.Keys))
        {
            var propDiff = DiffProperty(prevProps[name], currProps[name]);
            if (propDiff != null)
                diff.ModifiedProperties.Add(propDiff);
        }

        // Relationships, as declared, for the same reason.
        var prevRels = ToSafeDictionary(prev.Relationships.Where(r => r.InheritedFrom is null), r => r.PropertyName);
        var currRels = ToSafeDictionary(curr.Relationships.Where(r => r.InheritedFrom is null), r => r.PropertyName);

        foreach (var name in currRels.Keys.Except(prevRels.Keys))
            diff.AddedRelationships.Add($"{name} -> {currRels[name].RelatedEntity} ({currRels[name].Type})");

        foreach (var name in prevRels.Keys.Except(currRels.Keys))
            diff.RemovedRelationships.Add($"{name} -> {prevRels[name].RelatedEntity} ({prevRels[name].Type})");

        // Validation rules, as declared. An entity carries the ones it inherits so that reading it
        // tells the whole truth, but a change has one author: editing a rule on an audit base
        // would otherwise be reported again under every entity in the application, burying the one
        // line that says where it was actually changed.
        var prevRules = prev.ValidationRules.Where(r => r.InheritedFrom is null).Select(FormatValidationRule).ToHashSet();
        var currRules = curr.ValidationRules.Where(r => r.InheritedFrom is null).Select(FormatValidationRule).ToHashSet();

        foreach (var rule in currRules.Except(prevRules))
            diff.AddedValidationRules.Add(rule);
        foreach (var rule in prevRules.Except(currRules))
            diff.RemovedValidationRules.Add(rule);

        // Appearance rules, on the same terms.
        var prevAppRules = prev.AppearanceRules.Where(r => r.InheritedFrom is null).Select(FormatAppearanceRule).ToHashSet();
        var currAppRules = curr.AppearanceRules.Where(r => r.InheritedFrom is null).Select(FormatAppearanceRule).ToHashSet();

        foreach (var rule in currAppRules.Except(prevAppRules))
            diff.AddedAppearanceRules.Add(rule);
        foreach (var rule in prevAppRules.Except(currAppRules))
            diff.RemovedAppearanceRules.Add(rule);

        return diff;
    }

    /// <summary>
    /// Compares two properties and returns field-level changes.
    /// </summary>
    private PropertyDiff? DiffProperty(ExtractedProperty prev, ExtractedProperty curr)
    {
        var changes = new List<FieldChange>();

        CompareField(changes, "TypeName", prev.TypeName, curr.TypeName);
        CompareField(changes, "Description", prev.Description, curr.Description);
        CompareField(changes, "IsRequired", prev.IsRequired.ToString(), curr.IsRequired.ToString());
        CompareField(changes, "IsComputed", prev.IsComputed.ToString(), curr.IsComputed.ToString());
        CompareField(changes, "DisplayName", prev.DisplayName, curr.DisplayName);
        CompareField(changes, "Size", prev.Size?.ToString(), curr.Size?.ToString());
        CompareField(changes, "PersistentAlias", prev.PersistentAlias, curr.PersistentAlias);
        CompareField(changes, "DataSourceCriteria", prev.DataSourceCriteria, curr.DataSourceCriteria);
        CompareField(changes, "DefaultValue", prev.DefaultValue, curr.DefaultValue);
        CompareField(changes, "EditorAlias", prev.EditorAlias, curr.EditorAlias);

        if (changes.Count == 0) return null;

        return new PropertyDiff { PropertyName = curr.Name, Changes = changes };
    }

    /// <summary>
    /// Compares controller collections and updates controller summary counters.
    /// </summary>
    private void DiffControllers(ExtractedProject prev, ExtractedProject curr, ProjectDiffReport report)
    {
        var prevMap = ToSafeDictionary(prev.Controllers, c => c.ClassName);
        var currMap = ToSafeDictionary(curr.Controllers, c => c.ClassName);

        foreach (var name in currMap.Keys.Except(prevMap.Keys))
            report.ControllerChanges.Added.Add(name);

        foreach (var name in prevMap.Keys.Except(currMap.Keys))
            report.ControllerChanges.Removed.Add(name);

        foreach (var name in prevMap.Keys.Intersect(currMap.Keys))
        {
            var diff = DiffController(prevMap[name], currMap[name]);
            if (diff.HasChanges)
                report.ControllerChanges.Modified.Add(diff);
        }

        report.Summary.ControllersAdded = report.ControllerChanges.Added.Count;
        report.Summary.ControllersRemoved = report.ControllerChanges.Removed.Count;
        report.Summary.ControllersModified = report.ControllerChanges.Modified.Count;
    }

    /// <summary>
    /// Produces a detailed diff for one controller.
    /// </summary>
    private ControllerDiff DiffController(ExtractedController prev, ExtractedController curr)
    {
        var diff = new ControllerDiff { ClassName = curr.ClassName };

        // Target changes
        if (prev.TargetObjectType != curr.TargetObjectType)
            diff.TargetObjectTypeChange = new FieldChange { FieldName = "TargetObjectType", OldValue = prev.TargetObjectType, NewValue = curr.TargetObjectType };

        if (prev.TargetViewType != curr.TargetViewType)
            diff.TargetViewTypeChange = new FieldChange { FieldName = "TargetViewType", OldValue = prev.TargetViewType, NewValue = curr.TargetViewType };

        // Actions
        var prevActions = ToSafeDictionary(prev.Actions, a => a.ActionId);
        var currActions = ToSafeDictionary(curr.Actions, a => a.ActionId);

        foreach (var id in currActions.Keys.Except(prevActions.Keys))
            diff.AddedActions.Add($"{id} ({currActions[id].ActionType}) - {currActions[id].Caption}");

        foreach (var id in prevActions.Keys.Except(currActions.Keys))
            diff.RemovedActions.Add($"{id} ({prevActions[id].ActionType}) - {prevActions[id].Caption}");

        foreach (var id in prevActions.Keys.Intersect(currActions.Keys))
        {
            var actionDiff = DiffAction(prevActions[id], currActions[id]);
            if (actionDiff != null)
                diff.ModifiedActions.Add(actionDiff);
        }

        return diff;
    }

    /// <summary>
    /// Produces action-level field changes for one action pair.
    /// </summary>
    private ActionDiff? DiffAction(ExtractedAction prev, ExtractedAction curr)
    {
        var changes = new List<FieldChange>();

        CompareField(changes, "Caption", prev.Caption, curr.Caption);
        CompareField(changes, "Category", prev.Category, curr.Category);
        CompareField(changes, "ConfirmationMessage", prev.ConfirmationMessage, curr.ConfirmationMessage);
        CompareField(changes, "ImageName", prev.ImageName, curr.ImageName);
        CompareField(changes, "ToolTip", prev.ToolTip, curr.ToolTip);
        CompareField(changes, "EnabledCriteria", prev.EnabledCriteria, curr.EnabledCriteria);

        // Detect handler body change (important for business logic tracking)
        if (prev.ExecuteMethodBody != curr.ExecuteMethodBody)
            changes.Add(new FieldChange { FieldName = "ExecuteHandler", OldValue = "(changed)", NewValue = "(changed)" });

        if (changes.Count == 0) return null;

        return new ActionDiff { ActionId = curr.ActionId, Changes = changes };
    }

    /// <summary>
    /// Compares navigation groups and updates navigation summary counters.
    /// </summary>
    private void DiffNavigation(ExtractedProject prev, ExtractedProject curr, ProjectDiffReport report)
    {
        var prevMap = ToSafeDictionary(prev.Navigation, n => n.GroupName);
        var currMap = ToSafeDictionary(curr.Navigation, n => n.GroupName);

        foreach (var name in currMap.Keys.Except(prevMap.Keys))
            report.NavigationChanges.Added.Add(name);

        foreach (var name in prevMap.Keys.Except(currMap.Keys))
            report.NavigationChanges.Removed.Add(name);

        foreach (var name in prevMap.Keys.Intersect(currMap.Keys))
        {
            var prevEntities = prevMap[name].EntityClassNames.ToHashSet();
            var currEntities = currMap[name].EntityClassNames.ToHashSet();

            var added = currEntities.Except(prevEntities).ToList();
            var removed = prevEntities.Except(currEntities).ToList();

            if (added.Count > 0 || removed.Count > 0)
            {
                report.NavigationChanges.Modified.Add(new NavigationDiff
                {
                    GroupName = name,
                    AddedEntities = added,
                    RemovedEntities = removed
                });
            }
        }

        report.Summary.NavigationAdded = report.NavigationChanges.Added.Count;
        report.Summary.NavigationRemoved = report.NavigationChanges.Removed.Count;
        report.Summary.NavigationModified = report.NavigationChanges.Modified.Count;
    }

    /// <summary>
    /// Compares seed-data method names and updates summary counters.
    /// </summary>
    private void DiffSeedData(ExtractedProject prev, ExtractedProject curr, ProjectDiffReport report)
    {
        var prevMethods = prev.SeedData.Select(s => s.MethodName).ToHashSet();
        var currMethods = curr.SeedData.Select(s => s.MethodName).ToHashSet();

        foreach (var name in currMethods.Except(prevMethods))
            report.SeedDataChanges.Added.Add(name);

        foreach (var name in prevMethods.Except(currMethods))
            report.SeedDataChanges.Removed.Add(name);

        report.Summary.SeedDataAdded = report.SeedDataChanges.Added.Count;
        report.Summary.SeedDataRemoved = report.SeedDataChanges.Removed.Count;
    }

    /// <summary>
    /// Compares model editor metadata when available.
    /// </summary>
    private void DiffModelEditor(ExtractedProject prev, ExtractedProject curr, ProjectDiffReport report)
    {
        if (prev.ModelEditorInfo == null && curr.ModelEditorInfo == null) return;

        var diff = new ModelEditorDiff();

        var prevViews = ToSafeDictionary(prev.ModelEditorInfo?.Views ?? [], v => v.Id);
        var currViews = ToSafeDictionary(curr.ModelEditorInfo?.Views ?? [], v => v.Id);

        foreach (var id in currViews.Keys.Except(prevViews.Keys))
            diff.AddedViews.Add(id);
        foreach (var id in prevViews.Keys.Except(currViews.Keys))
            diff.RemovedViews.Add(id);
        foreach (var id in prevViews.Keys.Intersect(currViews.Keys))
        {
            var p = prevViews[id];
            var c = currViews[id];
            if (p.Caption != c.Caption || p.AllowEdit != c.AllowEdit || p.AllowDelete != c.AllowDelete ||
                p.AllowNew != c.AllowNew || p.Criteria != c.Criteria ||
                p.Columns.Count != c.Columns.Count || p.Filters.Count != c.Filters.Count)
                diff.ModifiedViews.Add(id);
        }

        var prevClasses = ToSafeDictionary(prev.ModelEditorInfo?.BOModelClasses ?? [], c => c.FullName);
        var currClasses = ToSafeDictionary(curr.ModelEditorInfo?.BOModelClasses ?? [], c => c.FullName);

        foreach (var name in currClasses.Keys.Except(prevClasses.Keys))
            diff.AddedBOClasses.Add(name);
        foreach (var name in prevClasses.Keys.Except(currClasses.Keys))
            diff.RemovedBOClasses.Add(name);
        foreach (var name in prevClasses.Keys.Intersect(currClasses.Keys))
        {
            var p = prevClasses[name];
            var c = currClasses[name];
            if (p.Caption != c.Caption || p.IsCloneable != c.IsCloneable)
                diff.ModifiedBOClasses.Add(name);
        }

        if (diff.HasChanges)
            report.ModelEditorChanges = diff;
    }

    private static string FormatValidationRule(ExtractedValidationRule rule)
        => $"{rule.RuleType}:{rule.TargetProperty ?? "*"}:{rule.TargetCriteria ?? ""}";

    /// <summary>
    /// What makes two appearance rules the same rule, for the purpose of reporting a change.
    /// </summary>
    /// <remarks>
    /// The identifier alone was the whole key, and it failed three ways. An empty identifier is
    /// ordinary rather than a mistake — a rule written on a property already says what it governs —
    /// so every unnamed rule in an application collapsed into one entry, and adding or removing one
    /// of them was reported as no change at all. A rule whose criteria was edited kept its
    /// identifier, so the edit reported as nothing. So did a rule that changed from disabling a
    /// field to hiding it, which is the whole of what an appearance rule is for.
    /// <para>
    /// Written out as a phrase rather than a colon-separated key because this string is not only
    /// compared — it is what the diff report prints.
    /// </para>
    /// </remarks>
    private static string FormatAppearanceRule(ExtractedAppearanceRule rule)
    {
        var effects = new List<string>();

        if (!string.IsNullOrWhiteSpace(rule.Visibility)) effects.Add($"visibility={rule.Visibility}");
        if (!string.IsNullOrWhiteSpace(rule.Enabled)) effects.Add($"enabled={rule.Enabled}");
        if (!string.IsNullOrWhiteSpace(rule.BackColor)) effects.Add($"back colour={rule.BackColor}");
        if (!string.IsNullOrWhiteSpace(rule.FontColor)) effects.Add($"font colour={rule.FontColor}");

        var name = rule.Id is { Length: > 0 } id ? id : "(unnamed)";
        var when = rule.Criteria is { Length: > 0 } criteria ? $"when {criteria}" : "always";
        var effect = effects.Count > 0 ? string.Join(", ", effects) : "no declared effect";

        // The item type is part of the key: a rule repointed from a column to an action of the same
        // name governs something else entirely, and nothing else in this string would move.
        var kind = rule.AppearanceItemType is { Length: > 0 } itemType ? $" [{itemType}]" : "";

        return $"{name} on {rule.TargetItems ?? "*"}{kind} {when}: {effect}";
    }

    private static void CompareField(List<FieldChange> changes, string fieldName, string? oldVal, string? newVal)
    {
        if (oldVal != newVal)
            changes.Add(new FieldChange { FieldName = fieldName, OldValue = oldVal, NewValue = newVal });
    }

    /// <summary>Safe ToDictionary that handles duplicate keys by keeping the last value.</summary>
    private static Dictionary<string, T> ToSafeDictionary<T>(IEnumerable<T> source, Func<T, string> keySelector)
        => source.GroupBy(keySelector).ToDictionary(g => g.Key, g => g.Last());
}
