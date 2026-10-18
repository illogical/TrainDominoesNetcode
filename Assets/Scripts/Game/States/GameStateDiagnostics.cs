using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Assets.Scripts.Game.States
{
    public enum StateTraceLevel { Off, Transitions, Verbose }
    public enum StateTraceKind { Transition, Begin, End, Fault }

    /// <summary>Immutable local diagnostics. Contains no hands, board snapshots, or rule decisions.</summary>
    public sealed class StateTraceRecord
    {
        public long Sequence { get; }
        public DateTime TimestampUtc { get; }
        public int Frame { get; }
        public StateTraceKind Kind { get; }
        public string FromState { get; }
        public string ToState { get; }
        public string Operation { get; }
        public string Detail { get; }

        internal StateTraceRecord(long sequence, StateTraceKind kind, string fromState,
            string toState, string operation, string detail)
        {
            Sequence = sequence;
            TimestampUtc = DateTime.UtcNow;
            Frame = Time.frameCount;
            Kind = kind;
            FromState = fromState;
            ToState = toState;
            Operation = operation;
            Detail = detail;
        }

        public override string ToString()
            => $"[PlayerState #{Sequence} {TimestampUtc:O} frame={Frame}] {Kind} " +
               $"{FromState ?? "<none>"} -> {ToState ?? "<none>"} {Operation} {Detail}";
    }

    /// <summary>Bounded per-context history; observers can read snapshots without controlling play.</summary>
    public sealed class GameStateDiagnostics
    {
        public StateTraceLevel Level { get; set; } = StateTraceLevel.Transitions;
        public string ExecutingStateName { get; private set; }
        public string ExecutingOperation { get; private set; }
        public IReadOnlyList<StateTraceRecord> History => history.ToArray();

        private const int HistoryCapacity = 128;
        private readonly Queue<StateTraceRecord> history = new Queue<StateTraceRecord>(HistoryCapacity);
        private readonly Action<string> log;
        private long sequence;

        public GameStateDiagnostics(Action<string> log) { this.log = log; }

        internal void RecordTransition(GameStateBase previous, GameStateBase next, string reason)
        {
            if (Level == StateTraceLevel.Off) return;
            Record(StateTraceKind.Transition, previous?.Name, next?.Name, reason, null);
        }

        internal void RecordFault(GameStateBase state, string operation, Exception exception)
        {
            if (Level == StateTraceLevel.Off) return;
            Record(StateTraceKind.Fault, state.Name, state.Name, operation, exception.ToString());
        }

        public IDisposable TraceExecution(GameStateBase state, string operation)
        {
            if (Level != StateTraceLevel.Verbose) return EmptyScope.Instance;
            return new ExecutionScope(this, state.Name, operation);
        }

        private void Record(StateTraceKind kind, string from, string to, string operation, string detail)
        {
            var record = new StateTraceRecord(++sequence, kind, from, to, operation, detail);
            if (history.Count == HistoryCapacity) history.Dequeue();
            history.Enqueue(record);
            log?.Invoke(record.ToString());
        }

        private sealed class EmptyScope : IDisposable
        {
            internal static readonly EmptyScope Instance = new EmptyScope();
            public void Dispose() { }
        }

        private sealed class ExecutionScope : IDisposable
        {
            private readonly GameStateDiagnostics owner;
            private readonly string state, operation, previousState, previousOperation;
            private readonly long started;
            private bool disposed;

            internal ExecutionScope(GameStateDiagnostics owner, string state, string operation)
            {
                this.owner = owner;
                this.state = state;
                this.operation = operation;
                previousState = owner.ExecutingStateName;
                previousOperation = owner.ExecutingOperation;
                owner.ExecutingStateName = state;
                owner.ExecutingOperation = operation;
                started = Stopwatch.GetTimestamp();
                owner.Record(StateTraceKind.Begin, state, state, operation, null);
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                owner.ExecutingStateName = previousState;
                owner.ExecutingOperation = previousOperation;
                if (owner.Level != StateTraceLevel.Verbose) return;
                double milliseconds = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
                owner.Record(StateTraceKind.End, state, state, operation, $"elapsedMs={milliseconds:F3}");
            }
        }
    }
}
