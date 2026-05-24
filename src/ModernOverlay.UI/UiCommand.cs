namespace ModernOverlay.UI;

/// <summary>
/// Represents an executable UI action with optional availability state.
/// </summary>
public sealed class UiCommand
{
    private readonly Action<object?> execute;
    private readonly Func<object?, bool>? canExecute;

    /// <summary>
    /// Initializes a new command.
    /// </summary>
    /// <param name="execute">The action to execute.</param>
    /// <param name="canExecute">An optional predicate that controls whether the command is available.</param>
    public UiCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.canExecute = canExecute;
    }

    /// <summary>
    /// Occurs when command availability should be re-evaluated by bound controls.
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Creates a command from a parameterless action.
    /// </summary>
    /// <param name="execute">The action to execute.</param>
    /// <returns>A command that invokes <paramref name="execute"/>.</returns>
    public static UiCommand FromAction(Action execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return new UiCommand(_ => execute());
    }

    /// <summary>
    /// Determines whether the command can execute for the supplied parameter.
    /// </summary>
    /// <param name="parameter">The optional command parameter.</param>
    /// <returns><see langword="true"/> when the command is available; otherwise, <see langword="false"/>.</returns>
    public bool CanExecute(object? parameter = null) => canExecute?.Invoke(parameter) ?? true;

    /// <summary>
    /// Executes the command when <see cref="CanExecute(object?)"/> allows it.
    /// </summary>
    /// <param name="parameter">The optional command parameter.</param>
    public void Execute(object? parameter = null)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        execute(parameter);
    }

    /// <summary>
    /// Raises <see cref="CanExecuteChanged"/> for controls subscribed to this command.
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
