using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Services;

/// <summary>
/// The undo steps recorded during registration, run in reverse with each one isolated from the rest.
/// </summary>
/// <remarks>
/// <para>
/// A bare pop-and-invoke loop is only safe while every step is a detach that cannot throw. Once one
/// of them tears down a socket that assumption stops holding, and a single throw abandons every step
/// still on the stack — leaving windows registered against a disposed plugin, which the host reports
/// only on the <em>next</em> enable rather than at the throw.
/// </para>
/// <para>
/// Isolation is not reordering. Steps still run strictly in reverse of the order they were pushed,
/// because that ordering carries real constraints: a frame handler has to detach before what it
/// draws goes away. Each step is wrapped, not moved.
/// </para>
/// <para>
/// Nothing is swallowed. A step that throws is handed to the caller's reporter with the name it was
/// pushed under, because a teardown that hides its own failure is a worse defect than one that stops
/// early: it trades a loud host-level complaint for a plugin that is quietly half unwound.
/// </para>
/// </remarks>
public sealed class TeardownSequence
{
    private readonly Stack<(string Name, Action Undo)> _steps = new();

    /// <summary>
    /// Records how to undo a registration that has just completed.
    /// </summary>
    /// <param name="name">
    /// What this step undoes, for the log. It is the only thing identifying the step when it fails,
    /// since the step itself is a closure with no name of its own.
    /// </param>
    /// <param name="undo">The undo action. Runs at most once.</param>
    public void Push(string name, Action undo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(undo);

        _steps.Push((name, undo));
    }

    /// <summary>
    /// Runs every recorded step in reverse of the order it was pushed, continuing past any that
    /// throw, and empties the sequence.
    /// </summary>
    /// <param name="onStepFailed">
    /// Receives the name and the exception of each step that threw. Called once per failure, as the
    /// failure happens, so the log keeps the order things actually failed in.
    /// </param>
    /// <remarks>
    /// A reporter that throws is not itself isolated, deliberately: there would be nowhere left to
    /// report that failure to, and catching it would be the silent swallow this type exists to
    /// avoid. Callers pass a logger, and a logger that throws is a defect in the caller.
    /// </remarks>
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
