using System;
using System.Collections.Generic;
using Minecraft.Combat;

namespace Minecraft.GoalPlanning
{
    public readonly struct GoapReservationKey : IEquatable<GoapReservationKey>
    {
        public readonly GoapReservationKind Kind;
        public readonly string TargetId;

        public GoapReservationKey(GoapReservationKind kind, string targetId)
        {
            Kind = kind;
            TargetId = targetId ?? string.Empty;
        }

        public bool Equals(GoapReservationKey other)
        {
            return Kind == other.Kind && string.Equals(TargetId, other.TargetId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is GoapReservationKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(TargetId);
            }
        }

        public override string ToString()
        {
            return $"{Kind}:{TargetId}";
        }
    }

    public readonly struct GoapReservationClaim
    {
        public readonly string OwnerId;
        public readonly GoapReservationKey Key;
        public readonly float ClaimedAtSec;
        public readonly float ExpiresAtSec;

        public GoapReservationClaim(string ownerId, in GoapReservationKey key, float claimedAtSec, float expiresAtSec)
        {
            OwnerId = ownerId;
            Key = key;
            ClaimedAtSec = claimedAtSec;
            ExpiresAtSec = expiresAtSec;
        }

        public bool IsExpired(float nowSec)
        {
            return nowSec >= ExpiresAtSec;
        }
    }

    public interface IGoapReservationStore
    {
        bool TryClaim(in GoapReservationKey key, string ownerId, float nowSec, float timeoutSec, out GoapReservationClaim claim);
        void Release(in GoapReservationKey key, string ownerId);
        void ReleaseAllForOwner(string ownerId);
        bool IsReservedByOther(in GoapReservationKey key, string ownerId, float nowSec);
        int SweepExpired(float nowSec);
    }

    public sealed class InMemoryGoapReservationStore : IGoapReservationStore
    {
        private readonly Dictionary<GoapReservationKey, GoapReservationClaim> m_Claims
            = new Dictionary<GoapReservationKey, GoapReservationClaim>();

        public bool TryClaim(in GoapReservationKey key, string ownerId, float nowSec, float timeoutSec, out GoapReservationClaim claim)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                claim = default;
                return false;
            }

            SweepExpired(nowSec);

            if (m_Claims.TryGetValue(key, out GoapReservationClaim current))
            {
                if (!string.Equals(current.OwnerId, ownerId, StringComparison.Ordinal))
                {
                    claim = current;
                    return false;
                }
            }

            float expireAt = nowSec + Math.Max(0.05f, timeoutSec);
            claim = new GoapReservationClaim(ownerId, key, nowSec, expireAt);
            m_Claims[key] = claim;
            return true;
        }

        public void Release(in GoapReservationKey key, string ownerId)
        {
            if (!m_Claims.TryGetValue(key, out GoapReservationClaim current))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(ownerId) || string.Equals(current.OwnerId, ownerId, StringComparison.Ordinal))
            {
                m_Claims.Remove(key);
            }
        }

        public void ReleaseAllForOwner(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return;
            }

            List<GoapReservationKey> toRemove = null;
            foreach (KeyValuePair<GoapReservationKey, GoapReservationClaim> pair in m_Claims)
            {
                if (!string.Equals(pair.Value.OwnerId, ownerId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (toRemove == null)
                {
                    toRemove = new List<GoapReservationKey>();
                }

                toRemove.Add(pair.Key);
            }

            if (toRemove == null)
            {
                return;
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                m_Claims.Remove(toRemove[i]);
            }
        }

        public bool IsReservedByOther(in GoapReservationKey key, string ownerId, float nowSec)
        {
            if (!m_Claims.TryGetValue(key, out GoapReservationClaim claim))
            {
                return false;
            }

            if (claim.IsExpired(nowSec))
            {
                m_Claims.Remove(key);
                return false;
            }

            return !string.Equals(claim.OwnerId, ownerId, StringComparison.Ordinal);
        }

        public int SweepExpired(float nowSec)
        {
            List<GoapReservationKey> expired = null;
            foreach (KeyValuePair<GoapReservationKey, GoapReservationClaim> pair in m_Claims)
            {
                if (!pair.Value.IsExpired(nowSec))
                {
                    continue;
                }

                if (expired == null)
                {
                    expired = new List<GoapReservationKey>();
                }

                expired.Add(pair.Key);
            }

            if (expired == null)
            {
                return 0;
            }

            for (int i = 0; i < expired.Count; i++)
            {
                m_Claims.Remove(expired[i]);
            }

            return expired.Count;
        }
    }

}
