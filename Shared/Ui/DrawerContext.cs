namespace Sacco_Management_System.Shared.Ui;

/// <summary>Handed to a page that is shown inside the right-hand drawer, so it can close the drawer or report a save.</summary>
public sealed class DrawerContext
{
    public Func<Task> Saved { get; init; } = () => Task.CompletedTask;
    public Func<Task> Close { get; init; } = () => Task.CompletedTask;
}
