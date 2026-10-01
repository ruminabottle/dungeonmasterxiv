using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Services;

public sealed class TeardownSequence
{
    private readonly Stack<(string Name, Action Undo)> _steps = new();

    public void Push(string name, Action undo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(undo);

        _steps.Push((name, undo));
    }

    public void UnwindAll(Action<string, Exception> onStepFailed)
    {
        ArgumentNullException.ThrowIfNull(onStepFailed);

        while (_steps.Count > 0)
        {
            var (name, undo) = _steps.Pop();

            try
            {
                undo();
            }
            catch (Exception exception)
            {
                onStepFailed(name, exception);
            }
        }
    }
}
