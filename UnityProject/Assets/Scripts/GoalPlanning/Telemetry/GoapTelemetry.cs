using System;
using System.Collections.Generic;
using Minecraft.Combat;

namespace Minecraft.GoalPlanning
{
    public enum GoapTelemetryEventType
    {
        GoalSwitch = 0,
        EmergencyInterrupt = 1,
        ReservationConflict = 2
    }

    public readonly struct GoapTelemetryEvent
    {
        public readonly GoapTelemetryEventType Type;
        public readonly float TimeSec;
        public readonly string Message;
        public readonly GoapGoalType GoalType;
        public readonly GoapActionType ActionType;
        public readonly string ReservationKey;

        public GoapTelemetryEvent(
            GoapTelemetryEventType type,
            float timeSec,
            string message,
            GoapGoalType goalType,
            GoapActionType actionType,
            string reservationKey)
        {
            Type = type;
            TimeSec = timeSec;
            Message = message;
            GoalType = goalType;
            ActionType = actionType;
            ReservationKey = reservationKey;
        }
    }

    public sealed class GoapTelemetryBuffer
    {
        private readonly Queue<GoapTelemetryEvent> m_Buffer;
        private readonly int m_Capacity;

        public GoapTelemetryBuffer(int capacity = 64)
        {
            m_Capacity = Math.Max(8, capacity);
            m_Buffer = new Queue<GoapTelemetryEvent>(m_Capacity);
        }

        public void Record(in GoapTelemetryEvent evt)
        {
            if (m_Buffer.Count >= m_Capacity)
            {
                m_Buffer.Dequeue();
            }

            m_Buffer.Enqueue(evt);
        }

        public GoapTelemetryEvent[] Snapshot()
        {
            return m_Buffer.ToArray();
        }

        public void Clear()
        {
            m_Buffer.Clear();
        }
    }

    public sealed class GoapTelemetryPanel
    {
        private readonly GoapTelemetryBuffer m_Source;

        public GoapTelemetryPanel(GoapTelemetryBuffer source)
        {
            m_Source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public string[] BuildRows(int maxRows = 12)
        {
            GoapTelemetryEvent[] snapshot = m_Source.Snapshot();
            if (snapshot.Length == 0)
            {
                return Array.Empty<string>();
            }

            int count = Math.Max(1, maxRows);
            int start = Math.Max(0, snapshot.Length - count);
            List<string> rows = new List<string>(snapshot.Length - start);
            for (int i = start; i < snapshot.Length; i++)
            {
                GoapTelemetryEvent evt = snapshot[i];
                rows.Add(
                    $"{evt.TimeSec:0.00}s | {evt.Type} | goal={evt.GoalType} action={evt.ActionType} | {evt.Message}");
            }

            return rows.ToArray();
        }
    }

}
