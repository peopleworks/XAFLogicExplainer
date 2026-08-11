using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// Updater blocks that ran once, on an upgrade, and never again.
/// </summary>
/// <remarks>
/// The only record of what happened to data in a released application. Reading the code that runs
/// today cannot recover it, which is what makes an agent invent a cause when asked why a column
/// holds what it holds.
/// </remarks>
public class MigrationTests
{
    private static List<ExtractedMigration> Migrations => SampleProjects.Demo.Migrations;

    [Fact]
    public void FindsEveryVersionGatedBlock()
    {
        Assert.Equal(2, Migrations.Count);
        Assert.Contains(Migrations, m => m.TargetVersion == "1.1.0.0");
        Assert.Contains(Migrations, m => m.TargetVersion == "1.2.0.0");
    }

    [Fact]
    public void ReadsBothBoundsOfTheCondition()
    {
        var migration = Migrations.Single(m => m.TargetVersion == "1.1.0.0");

        // `> 0.0.0.0` is how XAF teams say "an existing database, not a brand new one".
        Assert.Equal("0.0.0.0", migration.MinimumVersion);
        Assert.Contains("CurrentDBVersion", migration.Condition);
    }

    [Fact]
    public void RecordsWhichSchemaPhaseItRanIn()
    {
        // Not a detail: a block running before the schema changed cannot use the new columns.
        Assert.All(Migrations, m => Assert.Equal(MigrationPhase.BeforeSchemaUpdate, m.Phase));
    }

    [Fact]
    public void KeepsTheCommentExplainingWhy()
    {
        // The code says what it did; the comment is the only record of why, and why is the
        // question anyone reading a migration actually has.
        var migration = Migrations.Single(m => m.TargetVersion == "1.1.0.0");

        Assert.NotNull(migration.Description);
        Assert.Contains("expiry", migration.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NamesTheMethodsWhereTheWorkLives()
    {
        var migration = Migrations.Single(m => m.TargetVersion == "1.2.0.0");

        Assert.Contains("AssignCashToLegacyPayments", migration.CallsMethods);
        // Plumbing an updater always calls says nothing about the migration.
        Assert.DoesNotContain("CommitChanges", migration.CallsMethods);
    }

    [Fact]
    public void KeepsTheCodeThatRan()
    {
        var migration = Migrations.Single(m => m.TargetVersion == "1.1.0.0");

        Assert.Contains("BackfillPrescriptionExpiry", migration.Code);
    }

    [Fact]
    public void OrdinaryBranchingIsNotAMigration()
    {
        // The XPO sample's updater is full of `if (x == null)` guards around seeding. Only a
        // comparison against CurrentDBVersion means an upgrade happened.
        Assert.Empty(SampleProjects.Xpo.Migrations);
    }

    [Fact]
    public void SeedDataAndMigrationsAreDifferentThings()
    {
        // Seed data says what a fresh database contains; migrations say what happened to every
        // database that was not fresh. Conflating them would misreport both.
        Assert.NotEmpty(SampleProjects.Demo.SeedData);
        Assert.NotEmpty(SampleProjects.Demo.Migrations);
        Assert.DoesNotContain(SampleProjects.Demo.SeedData, s => s.MethodName == "BackfillPrescriptionExpiry");
    }

    [Fact]
    public void VersionsAreOrderedAsVersionsNotAsText()
    {
        // Sorted as text, "1.10.0.0" precedes "1.9.0.0", so the wrong bound would be reported the
        // moment an application reaches its tenth minor release.
        var condition = SyntaxOf("CurrentDBVersion < new Version(\"1.10.0.0\") && CurrentDBVersion > new Version(\"1.9.0.0\")");

        Assert.Equal("1.10.0.0", condition.TargetVersion);
        Assert.Equal("1.9.0.0", condition.MinimumVersion);
    }

    // ------------------------------------------------------------- surfacing

    [Fact]
    public void TheAgentContextExplainsDataTheCurrentCodeCannot()
    {
        var index = new AgentContextGenerator("0.10.1").GenerateIndex(SampleProjects.Demo, []);

        Assert.Contains("Data migrations", index);
        Assert.Contains("1.1.0.0", index);

        // "ran once" states history the source cannot prove: the guard establishes that a block
        // runs at most once for any database, not that a particular database ever passed through
        // it. The distinction is the difference between reporting intent and reporting an event.
        Assert.Contains("at most once", index);
        Assert.DoesNotContain("ran **once**", index);
    }

    [Fact]
    public void TheExplainerShowsWhatHappenedToTheData()
    {
        var html = new HtmlExplainerGenerator("0.10.1").Generate(SampleProjects.Demo);

        Assert.Contains("id=\"migrations\"", html);
        Assert.Contains("upgrading to 1.2.0.0", html);
        Assert.Contains("before the schema changed", html);
    }

    /// <summary>
    /// Runs the analyzer over a one-off updater, for conditions the fixture does not contain.
    /// </summary>
    private static ExtractedMigration SyntaxOf(string condition)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"xaflogic-mig-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(Path.Combine(directory, "Updater.cs"), $$"""
                using System;
                using DevExpress.ExpressApp.Updating;

                public class Updater : ModuleUpdater
                {
                    public override void UpdateDatabaseBeforeUpdateSchema()
                    {
                        if ({{condition}})
                        {
                            DoTheThing();
                        }
                    }
                }
                """);

            var migrations = new Core.Analyzers.UpdaterAnalyzer()
                .AnalyzeMigrations(directory, new ExtractionOptions());

            return Assert.Single(migrations);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
