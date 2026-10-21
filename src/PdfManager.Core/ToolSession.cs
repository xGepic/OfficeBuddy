namespace PdfManager.Core;

/// <summary>What the shared Tool screen frame needs from any session: undo/redo and unsaved changes.</summary>
public interface IToolSession
{
    bool CanUndo { get; }

    bool CanRedo { get; }

    /// <summary>
    /// Whether what the Tool shows differs from what was last saved (or opened). Undoing back to
    /// that point clears it; reversing a change by hand (moving a Document back) does not.
    /// </summary>
    bool HasUnsavedChanges { get; }

    /// <summary>Raised after every change, undo, redo and save.</summary>
    event EventHandler? Changed;

    void Undo();

    void Redo();
}

/// <summary>
/// What every Tool's session shares: undo/redo over immutable snapshots of its state.
/// </summary>
public abstract class ToolSession<TState>(TState initial) : IToolSession
    where TState : class?
{
    private readonly Stack<TState> _undo = new();
    private readonly Stack<TState> _redo = new();
    private TState _saved = initial;

    protected TState State { get; private set; } = initial;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public bool HasUnsavedChanges => !ReferenceEquals(State, _saved);

    public event EventHandler? Changed;

    public void Undo()
    {
        if (_undo.TryPop(out var previous))
        {
            _redo.Push(State);
            State = previous;
            OnChanged();
        }
    }

    public void Redo()
    {
        if (_redo.TryPop(out var next))
        {
            _undo.Push(State);
            State = next;
            OnChanged();
        }
    }

    /// <summary>Makes <paramref name="next"/> the current state, as one undoable user action.</summary>
    protected void Apply(TState next)
    {
        _undo.Push(State);
        _redo.Clear();
        State = next;
        OnChanged();
    }

    /// <summary>Starts over from <paramref name="state"/>, e.g. a newly opened Document: nothing to undo or save.</summary>
    protected void Reset(TState state)
    {
        State = state;
        _saved = state;
        _undo.Clear();
        _redo.Clear();
        OnChanged();
    }

    /// <summary>
    /// Records that <paramref name="saved"/>, a state this session had, was written to disk. The saved
    /// result is final, so the undo history ends here.
    /// </summary>
    protected void MarkSaved(TState saved)
    {
        _saved = saved;
        _undo.Clear();
        _redo.Clear();
        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
