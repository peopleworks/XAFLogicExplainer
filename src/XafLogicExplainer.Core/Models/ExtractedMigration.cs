namespace XafLogicExplainer.Core.Models;

/// <summary>
/// A block of the updater that runs only when an existing database is upgraded past a version.
/// </summary>
/// <remarks>
/// The answer to "why does this row look like that", and the one thing about an inherited
/// application that no amount of reading the current code can tell you. A migration runs once,
/// years ago, on somebody's production database — and then the code that did it sits in the
/// updater forever, never running again, describing a decision nobody remembers making.
/// <para>
/// Seed data says what a fresh database contains. This says what happened to every database that
/// was not fresh.
/// </para>
/// </remarks>
public class ExtractedMigration
{
    /// <summary>
    /// The version this block upgrades *to*: it runs when the database is older than this.
    /// </summary>
    public string? TargetVersion { get; set; }

    /// <summary>
    /// Lower bound, when the condition has one — nearly always <c>0.0.0.0</c>, which is how XAF
    /// teams say "an existing database, not a brand new one".
    /// </summary>
    public string? MinimumVersion { get; set; }

    /// <summary>The condition exactly as written.</summary>
    public string Condition { get; set; } = string.Empty;

    /// <summary>Whether it runs before or after the schema itself is updated.</summary>
    /// <remarks>
    /// Not a detail: a block that runs <em>before</em> the schema changes cannot use the new
    /// columns, and one that runs after cannot read what the change destroyed.
    /// </remarks>
    public MigrationPhase Phase { get; set; } = MigrationPhase.Unknown;

    /// <summary>Methods the block calls, which is usually where the work actually lives.</summary>
    public List<string> CallsMethods { get; set; } = [];

    /// <summary>
    /// The comment above the block.
    /// </summary>
    /// <remarks>
    /// Worth capturing above almost anything else here. A migration's code says what it did; the
    /// comment is the only record of why, and it is the question anyone reading it will have.
    /// </remarks>
    public string? Description { get; set; }

    /// <summary>The block body.</summary>
    public string Code { get; set; } = string.Empty;
}

/// <summary>When a migration block runs relative to the schema update.</summary>
public enum MigrationPhase
{
    /// <summary>Could not be established.</summary>
    Unknown,

    /// <summary>Before the schema changes — the old columns still exist, the new ones do not.</summary>
    BeforeSchemaUpdate,

    /// <summary>After the schema changes — the new columns exist, anything dropped is gone.</summary>
    AfterSchemaUpdate,
}
