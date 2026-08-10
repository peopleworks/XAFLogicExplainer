using System.Text;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Diff;

/// <summary>
/// Renders a <see cref="ProjectDiffReport"/> as human-readable markdown.
/// </summary>
public class DiffMarkdownGenerator
{
    private readonly string _lang;

    /// <summary>
    /// Creates a markdown diff renderer.
    /// </summary>
    /// <param name="languageCode">Language code for labels ("en" or "es").</param>
    public DiffMarkdownGenerator(string languageCode = "en")
    {
        _lang = languageCode;
    }

    /// <summary>
    /// Generates markdown content for a structured diff report.
    /// </summary>
    /// <param name="report">Diff report payload.</param>
    /// <returns>Markdown document with summary and detailed sections.</returns>
    public string Generate(ProjectDiffReport report)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"# {report.ProjectName} - {L("Change Report", "Reporte de Cambios")}");
        sb.AppendLine();
        sb.AppendLine($"> {L("Generated", "Generado")}: {report.GeneratedAt}  ");
        sb.AppendLine($"> {L("Previous hash", "Hash anterior")}: `{Truncate(report.PreviousHash, 16)}`  ");
        sb.AppendLine($"> {L("Current hash", "Hash actual")}: `{Truncate(report.CurrentHash, 16)}`");
        sb.AppendLine();

        // Summary
        sb.AppendLine($"## {L("Summary", "Resumen")}");
        sb.AppendLine();

        if (!report.HasChanges)
        {
            sb.AppendLine($"_{L("No changes detected.", "No se detectaron cambios.")}_");
            return sb.ToString();
        }

        var summary = report.Summary;

        if (summary.EntitiesAdded > 0 || summary.EntitiesRemoved > 0 || summary.EntitiesModified > 0)
            sb.AppendLine($"- **{L("Entities", "Entidades")}:** {FormatCounts(summary.EntitiesAdded, summary.EntitiesRemoved, summary.EntitiesModified)}");

        if (summary.ControllersAdded > 0 || summary.ControllersRemoved > 0 || summary.ControllersModified > 0)
            sb.AppendLine($"- **{L("Controllers", "Controladores")}:** {FormatCounts(summary.ControllersAdded, summary.ControllersRemoved, summary.ControllersModified)}");

        if (summary.NavigationAdded > 0 || summary.NavigationRemoved > 0 || summary.NavigationModified > 0)
            sb.AppendLine($"- **{L("Navigation", "Navegacion")}:** {FormatCounts(summary.NavigationAdded, summary.NavigationRemoved, summary.NavigationModified)}");

        if (summary.SeedDataAdded > 0 || summary.SeedDataRemoved > 0)
            sb.AppendLine($"- **{L("Seed Data", "Datos Semilla")}:** {FormatCounts(summary.SeedDataAdded, summary.SeedDataRemoved, 0)}");

        if (report.ModelEditorChanges?.HasChanges == true)
            sb.AppendLine($"- **Model Editor:** {L("changes detected", "cambios detectados")}");

        sb.AppendLine();

        // Entity changes
        if (report.EntityChanges.HasChanges)
        {
            sb.AppendLine("---");
            sb.AppendLine($"## {L("Entity Changes", "Cambios en Entidades")}");
            sb.AppendLine();

            foreach (var name in report.EntityChanges.Added)
                sb.AppendLine($"### + {name} ({L("Added", "Agregada")})");

            foreach (var name in report.EntityChanges.Removed)
                sb.AppendLine($"### - ~~{name}~~ ({L("Removed", "Eliminada")})");

            foreach (var entity in report.EntityChanges.Modified)
            {
                sb.AppendLine($"### ~ {entity.ClassName} ({L("Modified", "Modificada")})");
                sb.AppendLine();

                if (entity.DescriptionChange != null)
                    sb.AppendLine($"- {L("Description changed", "Descripcion cambiada")}: `{entity.DescriptionChange.OldValue ?? "(empty)"}` -> `{entity.DescriptionChange.NewValue ?? "(empty)"}`");

                if (entity.CaptionChange != null)
                    sb.AppendLine($"- {L("Caption changed", "Caption cambiado")}: `{entity.CaptionChange.OldValue ?? "(empty)"}` -> `{entity.CaptionChange.NewValue ?? "(empty)"}`");

                if (entity.BaseTypeChange != null)
                    sb.AppendLine($"- {L("Base type changed", "Tipo base cambiado")}: `{entity.BaseTypeChange.OldValue}` -> `{entity.BaseTypeChange.NewValue}`");

                foreach (var prop in entity.AddedProperties)
                    sb.AppendLine($"- **+** {L("Added property", "Propiedad agregada")}: `{prop}`");

                foreach (var prop in entity.RemovedProperties)
                    sb.AppendLine($"- **-** {L("Removed property", "Propiedad eliminada")}: ~~`{prop}`~~");

                foreach (var prop in entity.ModifiedProperties)
                {
                    var changeDescriptions = prop.Changes.Select(c => $"{c.FieldName}: `{c.OldValue ?? "null"}` -> `{c.NewValue ?? "null"}`");
                    sb.AppendLine($"- **~** {L("Modified property", "Propiedad modificada")}: `{prop.PropertyName}` - {string.Join(", ", changeDescriptions)}");
                }

                foreach (var rel in entity.AddedRelationships)
                    sb.AppendLine($"- **+** {L("Added relationship", "Relacion agregada")}: `{rel}`");

                foreach (var rel in entity.RemovedRelationships)
                    sb.AppendLine($"- **-** {L("Removed relationship", "Relacion eliminada")}: ~~`{rel}`~~");

                foreach (var rule in entity.AddedValidationRules)
                    sb.AppendLine($"- **+** {L("Added validation", "Validacion agregada")}: `{rule}`");

                foreach (var rule in entity.RemovedValidationRules)
                    sb.AppendLine($"- **-** {L("Removed validation", "Validacion eliminada")}: ~~`{rule}`~~");

                foreach (var rule in entity.AddedAppearanceRules)
                    sb.AppendLine($"- **+** {L("Added appearance rule", "Regla de apariencia agregada")}: `{rule}`");

                foreach (var rule in entity.RemovedAppearanceRules)
                    sb.AppendLine($"- **-** {L("Removed appearance rule", "Regla de apariencia eliminada")}: ~~`{rule}`~~");

                sb.AppendLine();
            }
        }

        // Controller changes
        if (report.ControllerChanges.HasChanges)
        {
            sb.AppendLine("---");
            sb.AppendLine($"## {L("Controller Changes", "Cambios en Controladores")}");
            sb.AppendLine();

            foreach (var name in report.ControllerChanges.Added)
                sb.AppendLine($"### + {name} ({L("Added", "Agregado")})");

            foreach (var name in report.ControllerChanges.Removed)
                sb.AppendLine($"### - ~~{name}~~ ({L("Removed", "Eliminado")})");

            foreach (var ctrl in report.ControllerChanges.Modified)
            {
                sb.AppendLine($"### ~ {ctrl.ClassName} ({L("Modified", "Modificado")})");
                sb.AppendLine();

                if (ctrl.TargetObjectTypeChange != null)
                    sb.AppendLine($"- {L("Target type changed", "Tipo objetivo cambiado")}: `{ctrl.TargetObjectTypeChange.OldValue}` -> `{ctrl.TargetObjectTypeChange.NewValue}`");

                foreach (var action in ctrl.AddedActions)
                    sb.AppendLine($"- **+** {L("Added action", "Accion agregada")}: `{action}`");

                foreach (var action in ctrl.RemovedActions)
                    sb.AppendLine($"- **-** {L("Removed action", "Accion eliminada")}: ~~`{action}`~~");

                foreach (var action in ctrl.ModifiedActions)
                {
                    var changes = action.Changes.Select(c => $"{c.FieldName}: `{c.OldValue ?? "null"}` -> `{c.NewValue ?? "null"}`");
                    sb.AppendLine($"- **~** {L("Modified action", "Accion modificada")}: `{action.ActionId}` - {string.Join(", ", changes)}");
                }

                sb.AppendLine();
            }
        }

        // Navigation changes
        if (report.NavigationChanges.HasChanges)
        {
            sb.AppendLine("---");
            sb.AppendLine($"## {L("Navigation Changes", "Cambios en Navegacion")}");
            sb.AppendLine();

            foreach (var name in report.NavigationChanges.Added)
                sb.AppendLine($"- **+** {L("Added group", "Grupo agregado")}: `{name}`");

            foreach (var name in report.NavigationChanges.Removed)
                sb.AppendLine($"- **-** {L("Removed group", "Grupo eliminado")}: ~~`{name}`~~");

            foreach (var nav in report.NavigationChanges.Modified)
            {
                sb.AppendLine($"- **~** `{nav.GroupName}`:");
                foreach (var entity in nav.AddedEntities)
                    sb.AppendLine($"  - **+** {entity}");
                foreach (var entity in nav.RemovedEntities)
                    sb.AppendLine($"  - **-** ~~{entity}~~");
            }
            sb.AppendLine();
        }

        // Seed data changes
        if (report.SeedDataChanges.HasChanges)
        {
            sb.AppendLine("---");
            sb.AppendLine($"## {L("Seed Data Changes", "Cambios en Datos Semilla")}");
            sb.AppendLine();

            foreach (var name in report.SeedDataChanges.Added)
                sb.AppendLine($"- **+** {L("Added method", "Metodo agregado")}: `{name}`");

            foreach (var name in report.SeedDataChanges.Removed)
                sb.AppendLine($"- **-** {L("Removed method", "Metodo eliminado")}: ~~`{name}`~~");

            sb.AppendLine();
        }

        // Model editor changes
        if (report.ModelEditorChanges?.HasChanges == true)
        {
            sb.AppendLine("---");
            sb.AppendLine($"## {L("Model Editor Changes", "Cambios en Model Editor")} (xafml)");
            sb.AppendLine();

            var me = report.ModelEditorChanges;

            if (me.AddedViews.Count > 0)
            {
                sb.AppendLine($"### {L("Views Added", "Vistas Agregadas")}");
                foreach (var v in me.AddedViews) sb.AppendLine($"- **+** `{v}`");
            }

            if (me.RemovedViews.Count > 0)
            {
                sb.AppendLine($"### {L("Views Removed", "Vistas Eliminadas")}");
                foreach (var v in me.RemovedViews) sb.AppendLine($"- **-** ~~`{v}`~~");
            }

            if (me.ModifiedViews.Count > 0)
            {
                sb.AppendLine($"### {L("Views Modified", "Vistas Modificadas")}");
                foreach (var v in me.ModifiedViews) sb.AppendLine($"- **~** `{v}`");
            }

            if (me.AddedBOClasses.Count > 0 || me.RemovedBOClasses.Count > 0 || me.ModifiedBOClasses.Count > 0)
            {
                sb.AppendLine($"### BOModel");
                foreach (var c in me.AddedBOClasses) sb.AppendLine($"- **+** `{c}`");
                foreach (var c in me.RemovedBOClasses) sb.AppendLine($"- **-** ~~`{c}`~~");
                foreach (var c in me.ModifiedBOClasses) sb.AppendLine($"- **~** `{c}`");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns a localized label for the configured language.
    /// </summary>
    private string L(string en, string es) => _lang == "es" ? es : en;

    /// <summary>
    /// Formats added/removed/modified counters into a compact label.
    /// </summary>
    private string FormatCounts(int added, int removed, int modified)
    {
        var parts = new List<string>();
        if (added > 0) parts.Add($"+{added} {L("added", "agregados")}");
        if (removed > 0) parts.Add($"-{removed} {L("removed", "eliminados")}");
        if (modified > 0) parts.Add($"~{modified} {L("modified", "modificados")}");
        return string.Join(", ", parts);
    }

    /// <summary>
    /// Truncates long hash strings for report readability.
    /// </summary>
    private static string Truncate(string value, int length)
        => value.Length > length ? value[..length] + "..." : value;
}
