using System;
using System.Collections.Generic;
using Minecraft.Combat;
using UnityEngine;

namespace Minecraft.Faction
{
    public enum FactionAssaultStage : byte
    {
        Assemble = 0,
        Advance = 1,
        Attack = 2,
        Regroup = 3,
    }

    [Serializable]
    public sealed class FactionSpawnAnchor
    {
        public string AnchorId = "anchor-01";
        public Transform AnchorTransform;
        public string FactionId = "hostile-assault-v1";
        public bool Enabled = true;
    }

    [Serializable]
    public sealed class FactionWaveDefinition
    {
        public string WaveId = "wave-01";
        [Min(1)] public int SpawnBudget = 6;
        [Min(1)] public int SpawnPerTick = 2;
        [Min(0.1f)] public float SpawnIntervalSec = 8f;
    }

    [Serializable]
    public sealed class FactionProfileDefinition
    {
        public string FactionId = "hostile-assault-v1";
        public bool Enabled = true;
        public bool IsPrimaryV1 = true;
        public GameObject AgentPrefab;
        public CombatActorRole CombatRole = CombatActorRole.Enemy;
        [Min(1)] public int AliveCap = 20;
        public List<FactionWaveDefinition> Waves = new List<FactionWaveDefinition>();
    }

    [Serializable]
    public sealed class FactionGoapPlannerPreset
    {
        [Header("GOAP Planner Contract (9 keys)")]
        [Range(1, 8)] public int MaxPlanDepth = 3;
        [Min(0.02f)] public float ReplanCooldownSec = 0.2f;
        [Min(0f)] public float GoalSwitchHysteresis = 0.75f;
        [Min(8)] public int PlannerBudgetPerTick = 64;
        [Range(0f, 1f)] public float EmergencyOverrideCoreHpPct = 0.25f;
        [Min(0)] public int ActionFailureRetryLimit = 2;
        [Min(0.1f)] public float PlanStaleTimeoutSec = 1.2f;
        [Min(0f)] public float HomeBiasWeight = 1f;
        [Min(0f)] public float AwaySafetyWeight = 1f;

        public GoalPlanning.GoapPlannerConfig ToConfig()
        {
            return new GoalPlanning.GoapPlannerConfig
            {
                MaxPlanDepth = Mathf.Max(1, MaxPlanDepth),
                ReplanCooldownSec = Mathf.Max(0.02f, ReplanCooldownSec),
                GoalSwitchHysteresis = Mathf.Max(0f, GoalSwitchHysteresis),
                PlannerBudgetPerTick = Mathf.Max(1, PlannerBudgetPerTick),
                EmergencyOverrideCoreHpPct = Mathf.Clamp01(EmergencyOverrideCoreHpPct),
                ActionFailureRetryLimit = Mathf.Max(0, ActionFailureRetryLimit),
                PlanStaleTimeoutSec = Mathf.Max(0.1f, PlanStaleTimeoutSec),
                HomeBiasWeight = Mathf.Max(0f, HomeBiasWeight),
                AwaySafetyWeight = Mathf.Max(0f, AwaySafetyWeight),
            };
        }
    }

    [CreateAssetMenu(menuName = "Minecraft/Faction/Objective Preset", fileName = "FactionObjectivePreset")]
    public sealed class FactionObjectivePreset : ScriptableObject
    {
        [Header("Target Priority")]
        [Min(0f)] public float PlayerTargetPriority = 1f;
        [Min(0f)] public float CoreTargetPriority = 0.7f;

        [Header("Regroup")]
        [Range(0f, 1f)] public float RegroupHealthThreshold = 0.3f;
        [Range(0f, 1f)] public float CasualtyRegroupThreshold = 0.4f;
        [Min(1)] public int RegroupFailureThreshold = 3;
        [Min(0.1f)] public float StageMinStaySeconds = 1.2f;

        [Header("Combat")]
        [Min(0.1f)] public float AttackRange = 1.8f;
        [Min(0.05f)] public float BrainTickIntervalSec = 0.2f;

        [Header("Territory")]
        [Min(1f)] public float HomeTerritoryRadius = 24f;
        public bool AllowAwayTerrainEdit = false;

        [Header("Reachability Probe")]
        [Min(0.1f)] public float RepathProbeIntervalSec = 0.6f;
        [Min(32)] public int ReachabilityIterations = 180;
        [Range(1, 16)] public int ReachabilitySearchRadius = 6;
    }

    public struct FactionRuntimeDirective
    {
        public string FactionId;
        public FactionAssaultStage Stage;
        public Transform PlayerTarget;
        public Transform CoreTarget;
        public Transform RegroupPoint;
        public float CoreHealthNormalized;
        public bool AllowAwayTerrainEdit;
        public Vector3 HomeCenter;
        public float HomeRadius;
        public FactionObjectivePreset ObjectivePreset;
    }
}
