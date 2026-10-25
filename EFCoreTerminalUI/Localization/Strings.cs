namespace EFCoreTerminalUI.Localization;

/// <summary>
/// Arayüz metinleri. Varsayılan değerler İngilizcedir; çeviriler yalnızca farklı olan alanları ezer,
/// eksik kalan alanlar İngilizce görünür. Metinler Spectre.Console markup içerebilir;
/// {0} yer tutucularına gelen değerler çağıran tarafta Markup.Escape ile kaçırılır.
/// </summary>
public sealed record Strings
{
    // Ana menü
    public string MenuPrompt { get; init; } = "[bold]What do you want to do?[/] [grey](↑/↓ and Enter)[/]";
    public string MenuAdd { get; init; } = "Add Migration";
    public string MenuRemove { get; init; } = "Remove Last Migration";
    public string MenuUpdate { get; init; } = "Update Database";
    public string MenuUpdateLatestHint { get; init; } = "(latest migration)";
    public string MenuUpdateToHint { get; init; } = "(pick migration / revert)";
    public string MenuScript { get; init; } = "Generate SQL Script";
    public string MenuRefresh { get; init; } = "Refresh list";
    public string MenuSettings { get; init; } = "Settings";
    public string MenuDrop { get; init; } = "Drop Database";
    public string MenuExit { get; init; } = "Exit";
    public string Back { get; init; } = "← Back";
    public string MoreChoices { get; init; } = "[grey](Move up and down to reveal more choices)[/]";

    // Ayarlar
    public string SettingsTitle { get; init; } = "[bold]Settings[/]";
    public string SettingsLanguage { get; init; } = "Language";
    public string SettingsProjects { get; init; } = "Change project / DbContext";
    public string PickLanguage { get; init; } = "[bold]Select language[/]";

    // Kurulum
    public string FolderNotFound { get; init; } = "[red]Folder not found:[/] {0}";
    public string CheckingEf { get; init; } = "Checking dotnet-ef...";
    public string EfNotFound { get; init; } = "dotnet-ef tool not found.";
    public string EfInstallConfirm { get; init; } = "Install it globally? (dotnet tool install -g dotnet-ef)";
    public string SolutionRootUsed { get; init; } = "[grey]Using solution folder:[/] {0}";
    public string NoProjects { get; init; } = "[red]No .csproj found in this folder:[/] {0}";
    public string NoProjectsHint { get; init; } = "Run efui in your solution folder.";
    public string PickMigrationProject { get; init; } = "[bold]Migration project[/] (where the DbContext / Migrations folder lives)";
    public string PickStartupProject { get; init; } = "[bold]Startup project[/] (the one with appsettings / Program.cs)";
    public string SuggestedFirst { get; init; } = "(suggested first)";
    public string LoadingContexts { get; init; } = "Building project, looking for DbContexts...";
    public string ContextListFailed { get; init; } = "Could not get the DbContext list.";
    public string NoContexts { get; init; } = "No DbContext found in the selected projects.";
    public string ChangeProjectsConfirm { get; init; } = "Do you want to change the project selection?";
    public string PickContext { get; init; } = "[bold]Select DbContext[/]";
    public string SettingsSaveFailed { get; init; } = "Could not save settings: {0}";

    public string MigrationsAssemblyMismatch { get; init; } = "[yellow]The migrations live in [bold]{0}[/], not in the selected migration project.[/]";
    public string SwitchMigrationProjectConfirm { get; init; } = "Switch the migration project to [bold]{0}[/] and retry?";

    // Liste / durum
    public string ReadingMigrations { get; init; } = "Reading migrations...";
    public string BuildingAndReading { get; init; } = "Building and reading migrations...";
    public string DbUnreachableRetry { get; init; } = "Could not connect to the database, reading offline...";
    public string MigrationListFailed { get; init; } = "Could not get the migration list.";
    public string LabelFolder { get; init; } = "Folder";
    public string LabelMigration { get; init; } = "Migration";
    public string LabelStartup { get; init; } = "Startup";
    public string LabelContext { get; init; } = "DbContext";
    public string LabelDatabase { get; init; } = "Database";
    public string DbConnected { get; init; } = "connected";
    public string DbNotConnected { get; init; } = "not reachable (status unknown)";
    public string NoMigrations { get; init; } = "No migrations yet.";
    public string ColMigration { get; init; } = "Migration";
    public string ColStatus { get; init; } = "Status";
    public string HiddenOlder { get; init; } = "{0} older migrations hidden";
    public string Latest { get; init; } = "◀ latest";
    public string Applied { get; init; } = "✔ applied";
    public string Pending { get; init; } = "● pending";
    public string PendingCount { get; init; } = "{0} migration(s) not applied to the database.";

    // Add
    public string MigrationNamePrompt { get; init; } = "Migration name [grey](leave empty to cancel)[/]:";
    public string MigrationNameInvalid { get; init; } = "[red]Use letters, digits and _ only[/]";
    public string UpdateNowConfirm { get; init; } = "Update the database now?";

    // Remove
    public string NothingToRemove { get; init; } = "No migration to remove.";
    public string WarningApplied { get; init; } = "[yellow]Warning:[/] [bold]{0}[/] is already applied to the database.";
    public string HowToContinue { get; init; } = "How do you want to continue?";
    public string RevertAndRemove { get; init; } = "Revert from database and remove (--force)";
    public string RemoveConfirm { get; init; } = "Remove [bold]{0}[/]?";

    // Update
    public string RevertAllOption { get; init; } = "0 — revert all migrations (empty database)";
    public string UpdateTargetPrompt { get; init; } = "Which migration should the database be at? [grey](newest first)[/]";
    public string RevertAllConfirm { get; init; } = "[red]All migrations will be reverted.[/] Are you sure?";

    // Script
    public string NoMigrationsForScript { get; init; } = "There are no migrations to script.";
    public string FromStart { get; init; } = "(from the beginning)";
    public string ScriptFromPrompt { get; init; } = "Start migration [grey](changes after this one)[/]";
    public string AlreadyLatest { get; init; } = "The selected migration is already the latest.";
    public string ScriptToPrompt { get; init; } = "End migration";
    public string IdempotentConfirm { get; init; } = "Generate an idempotent script?";
    public string OutputFilePrompt { get; init; } = "Output file:";
    public string ScriptWritten { get; init; } = "[green]Script written:[/] {0}";

    // Drop
    public string DropWarning { get; init; } = "[red bold]This will delete the database completely![/]";
    public string DropConfirmPrompt { get; init; } = "Type [red]DROP[/] to confirm:";

    // Çıktı / genel
    public string Done { get; init; } = "✔ Done";
    public string Failed { get; init; } = "✘ Failed (exit code {0})";
    public string EfOutputHeader { get; init; } = "dotnet ef output";
    public char Yes { get; init; } = 'y';
    public char No { get; init; } = 'n';
    public string InvalidYesNo { get; init; } = "[red]Enter y (yes) or n (no)[/]";
    public string PressAnyKey { get; init; } = "Press any key to continue...";
}
