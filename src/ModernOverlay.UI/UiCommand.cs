namespace ModernOverlay.UI;

public sealed class UiCommand
{
    private readonly Action<object?> execute;
    private readonly Func<object?, bool>? canExecute;

    public UiCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public static UiCommand FromAction(Action execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return new UiCommand(_ => execute());
    }

    public bool CanExecute(object? parameter = null) => canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter = null)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        execute(parameter);
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
