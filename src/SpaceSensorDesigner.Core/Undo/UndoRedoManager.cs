using System;
using System.Collections.Generic;

namespace SpaceSensorDesigner.Core.Undo;

/// <summary>A reversible edit. <see cref="Do"/> applies it; <see cref="Undo"/> reverts it.</summary>
public interface IUndoableAction
{
    string Label { get; }
    void Do();
    void Undo();
}

/// <summary>
/// A convenience action built from two lambdas, so view models can create undoable edits
/// inline without declaring a class per operation.
/// </summary>
public sealed class DelegateAction : IUndoableAction
{
    private readonly Action _do;
    private readonly Action _undo;

    public string Label { get; }

    public DelegateAction(string label, Action doAction, Action undoAction)
    {
        Label = label;
        _do = doAction;
        _undo = undoAction;
    }

    public void Do() => _do();
    public void Undo() => _undo();
}

/// <summary>
/// A standard two-stack undo/redo manager. Pushing (executing) a new action clears the redo stack.
/// Raises <see cref="Changed"/> whenever the available undo/redo state changes.
/// </summary>
public sealed class UndoRedoManager
{
    private readonly Stack<IUndoableAction> _undo = new();
    private readonly Stack<IUndoableAction> _redo = new();

    public event EventHandler? Changed;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public string? NextUndoLabel => _undo.Count > 0 ? _undo.Peek().Label : null;
    public string? NextRedoLabel => _redo.Count > 0 ? _redo.Peek().Label : null;

    /// <summary>Executes the action and records it for undo.</summary>
    public void Execute(IUndoableAction action)
    {
        action.Do();
        _undo.Push(action);
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Records an action whose effect has ALREADY been applied (e.g. a live drag-move), without
    /// calling <see cref="IUndoableAction.Do"/> again. Undo reverts it; a later Redo re-applies it.
    /// </summary>
    public void Push(IUndoableAction action)
    {
        _undo.Push(action);
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        var action = _undo.Pop();
        action.Undo();
        _redo.Push(action);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        var action = _redo.Pop();
        action.Do();
        _undo.Push(action);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
