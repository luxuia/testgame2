using System;
using System.Collections.Generic;
using UnityEngine;

namespace Minecraft.Combat
{
    public enum CombatTerritoryKind : byte
    {
        Home = 0,
        Away = 1,
    }

    public enum CombatActorRole : byte
    {
        Player = 0,
        Officer = 1,
        Puji = 2,
        Enemy = 3,
        Boss = 4,
    }

    public enum CombatActionKind : byte
    {
        Attack = 0,
        Skill = 1,
        Dodge = 2,
        BreakBlock = 3,
        PlaceBlock = 4,
    }

    public enum CombatSkillTriggerType : byte
    {
        Manual = 0,
        Auto = 1,
        Passive = 2,
        DirectorOverride = 3,
    }

    public enum CombatSkillTargetingType : byte
    {
        Self = 0,
        Entity = 1,
        Position = 2,
        Area = 3,
    }

    public enum CombatSkillEffectType : byte
    {
        Damage = 0,
        Heal = 1,
        BuffDebuff = 2,
        BreakBlock = 3,
        PlaceBlock = 4,
        Summon = 5,
    }

    [Serializable]
    public sealed class CombatRuntimeConfig
    {
        public const string HomeDamageBonusPctKey = "homeDamageBonusPct";
        public const string HomeDefenseBonusPctKey = "homeDefenseBonusPct";
        public const string AwayEditChargesKey = "awayEditCharges";
        public const string AwayEditRadiusKey = "awayEditRadius";
        public const string DodgeInvulnSecKey = "dodgeInvulnSec";
        public const string AutoTakeoverCoreHpPctKey = "autoTakeoverCoreHpPct";
        public const string AutoRepairIntervalSecKey = "autoRepairIntervalSec";
        public const string BossTerrainBreakIntervalSecKey = "bossTerrainBreakIntervalSec";
        public const string AdaptiveFailCountThresholdKey = "adaptiveFailCountThreshold";

        public static readonly string[] MvpKeys =
        {
            HomeDamageBonusPctKey,
            HomeDefenseBonusPctKey,
            AwayEditChargesKey,
            AwayEditRadiusKey,
            DodgeInvulnSecKey,
            AutoTakeoverCoreHpPctKey,
            AutoRepairIntervalSecKey,
            BossTerrainBreakIntervalSecKey,
            AdaptiveFailCountThresholdKey,
        };

        [Header("Territory Bonus")]
        public float HomeDamageBonusPct = 0.20f;
        public float HomeDefenseBonusPct = 0.15f;

        [Header("Away Edit")]
        public int AwayEditCharges = 3;
        public int AwayEditRadius = 2;

        [Header("Combat Timing")]
        public float DodgeInvulnSec = 0.25f;

        [Header("Auto Takeover")]
        public float AutoTakeoverCoreHpPct = 0.15f;
        public float AutoRepairIntervalSec = 1.0f;

        [Header("Boss")]
        public float BossTerrainBreakIntervalSec = 8.0f;

        [Header("Adaptive Difficulty")]
        public int AdaptiveFailCountThreshold = 2;

        public bool ValidateMvpKeyContract(ISet<string> providedKeys, out string error)
        {
            if (providedKeys == null)
            {
                error = "Provided key set is null.";
                return false;
            }

            if (providedKeys.Count != MvpKeys.Length)
            {
                error = $"Expected {MvpKeys.Length} keys, got {providedKeys.Count}.";
                return false;
            }

            for (int i = 0; i < MvpKeys.Length; i++)
            {
                if (!providedKeys.Contains(MvpKeys[i]))
                {
                    error = $"Missing required key: {MvpKeys[i]}";
                    return false;
                }
            }

            error = null;
            return true;
        }

        public bool ValidateValues(out string error)
        {
            if (AwayEditCharges < 0)
            {
                error = "AwayEditCharges cannot be negative.";
                return false;
            }

            if (AwayEditRadius < 0)
            {
                error = "AwayEditRadius cannot be negative.";
                return false;
            }

            if (DodgeInvulnSec < 0f || AutoRepairIntervalSec <= 0f || BossTerrainBreakIntervalSec <= 0f)
            {
                error = "Timing values must be non-negative and intervals must be positive.";
                return false;
            }

            if (AutoTakeoverCoreHpPct < 0f || AutoTakeoverCoreHpPct > 1f)
            {
                error = "AutoTakeoverCoreHpPct must be in range [0, 1].";
                return false;
            }

            if (AdaptiveFailCountThreshold < 0)
            {
                error = "AdaptiveFailCountThreshold cannot be negative.";
                return false;
            }

            error = null;
            return true;
        }
    }

    [Serializable]
    public struct CombatContext
    {
        public CombatTerritoryKind TerritoryKind;
        public bool HasTemporaryAwayEditAuthority;
        public bool DirectorForceTerrainAuthority;

        public bool CanEditTerrain()
        {
            return TerritoryKind == CombatTerritoryKind.Home
                   || HasTemporaryAwayEditAuthority
                   || DirectorForceTerrainAuthority;
        }

        public float GetDamageMultiplier(CombatRuntimeConfig config)
        {
            if (TerritoryKind != CombatTerritoryKind.Home || config == null)
            {
                return 1f;
            }

            return 1f + Mathf.Max(0f, config.HomeDamageBonusPct);
        }

        public float GetDefenseMultiplier(CombatRuntimeConfig config)
        {
            if (TerritoryKind != CombatTerritoryKind.Home || config == null)
            {
                return 1f;
            }

            return 1f + Mathf.Max(0f, config.HomeDefenseBonusPct);
        }
    }

    [Serializable]
    public sealed class CombatEntityRuntime
    {
        public int RuntimeId;
        public CombatActorRole Role;
        public string DisplayName;

        public float MaxHealth = 100f;
        public float CurrentHealth = 100f;
        public float AttackPower = 10f;
        public float Defense = 2f;

        public Vector3 Position;

        public bool IsAlive => CurrentHealth > 0f;

        public float ApplyDamage(float amount)
        {
            float clamped = Mathf.Max(0f, amount);
            CurrentHealth = Mathf.Max(0f, CurrentHealth - clamped);
            return clamped;
        }

        public float Heal(float amount)
        {
            float clamped = Mathf.Max(0f, amount);
            float before = CurrentHealth;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + clamped);
            return CurrentHealth - before;
        }
    }

    [Serializable]
    public sealed class CombatSkillPhaseDefinition
    {
        public float WindupSeconds = 0.05f;
        public float ExecuteSeconds = 0f;
        public float RecoverySeconds = 0.05f;

        public bool IsValid()
        {
            return WindupSeconds >= 0f && ExecuteSeconds >= 0f && RecoverySeconds >= 0f;
        }
    }

    [Serializable]
    public sealed class CombatSkillEffectDefinition
    {
        public CombatSkillEffectType EffectType;
        public float Magnitude = 1f;
        public float DurationSeconds;
        public int Count = 1;
    }

    [Serializable]
    public sealed class CombatSkillDefinition
    {
        public string SkillId = "skill.default";
        public CombatSkillTriggerType TriggerType = CombatSkillTriggerType.Manual;
        public CombatSkillTargetingType TargetingType = CombatSkillTargetingType.Entity;
        [Tooltip("Optional animation action name. Example: Punch, Jab, HeavySmash.")]
        public string AnimationActionName;
        public CombatSkillPhaseDefinition Phases = new CombatSkillPhaseDefinition();
        public float CooldownSeconds;
        public float Cost;
        public List<CombatSkillEffectDefinition> Effects = new List<CombatSkillEffectDefinition>();
    }

    public struct CombatActionRequest
    {
        public CombatActionKind ActionKind;
        public CombatContext Context;

        public CombatEntityRuntime Actor;
        public CombatEntityRuntime TargetEntity;
        public Vector3Int TargetBlock;

        public CombatSkillDefinition Skill;
        public string PreferredAnimationActionName;
        public bool TriggeredByAI;
    }

    public struct CombatActionResult
    {
        public bool Success;
        public string FailureReason;
        public float DamageDealt;
        public float HealingDone;
        public int SummonedCount;
        public bool TerrainEdited;
        public bool HadBuffOrDebuff;
        public string AnimationActionName;

        public static CombatActionResult Fail(string reason)
        {
            return new CombatActionResult
            {
                Success = false,
                FailureReason = reason,
                AnimationActionName = null,
            };
        }
    }

    public sealed class CombatActionPipeline
    {
        private readonly CombatRuntimeConfig m_Config;

        public CombatActionPipeline(CombatRuntimeConfig config)
        {
            m_Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public CombatActionResult Execute(in CombatActionRequest request)
        {
            if (request.Actor == null || !request.Actor.IsAlive)
            {
                return CombatActionResult.Fail("Actor is missing or dead.");
            }

            return request.ActionKind switch
            {
                CombatActionKind.Attack => ExecuteAttack(in request),
                CombatActionKind.Skill => ExecuteSkill(in request),
                CombatActionKind.Dodge => new CombatActionResult
                {
                    Success = true,
                    AnimationActionName = ResolveDefaultAnimationActionName(
                        CombatActionKind.Dodge,
                        request.PreferredAnimationActionName),
                },
                CombatActionKind.BreakBlock => ExecuteTerrainAction(in request, isPlace: false),
                CombatActionKind.PlaceBlock => ExecuteTerrainAction(in request, isPlace: true),
                _ => CombatActionResult.Fail("Unknown action."),
            };
        }

        private CombatActionResult ExecuteAttack(in CombatActionRequest request)
        {
            if (request.TargetEntity == null || !request.TargetEntity.IsAlive)
            {
                return CombatActionResult.Fail("Attack target is missing or dead.");
            }

            float damage = EvaluateDamage(
                request.Actor.AttackPower,
                request.TargetEntity.Defense,
                request.Context);
            request.TargetEntity.ApplyDamage(damage);

            return new CombatActionResult
            {
                Success = true,
                DamageDealt = damage,
                AnimationActionName = ResolveDefaultAnimationActionName(
                    CombatActionKind.Attack,
                    request.PreferredAnimationActionName),
            };
        }

        private CombatActionResult ExecuteSkill(in CombatActionRequest request)
        {
            if (request.Skill == null)
            {
                return CombatActionResult.Fail("Skill action has no skill definition.");
            }

            if (request.Skill.Phases == null || !request.Skill.Phases.IsValid())
            {
                return CombatActionResult.Fail("Skill phases are invalid.");
            }

            if (request.Skill.Effects == null || request.Skill.Effects.Count == 0)
            {
                return new CombatActionResult
                {
                    Success = true,
                    AnimationActionName = ResolveSkillAnimationActionName(in request),
                };
            }

            CombatActionResult aggregate = new CombatActionResult
            {
                Success = true,
                AnimationActionName = ResolveSkillAnimationActionName(in request),
            };
            for (int i = 0; i < request.Skill.Effects.Count; i++)
            {
                CombatSkillEffectDefinition effect = request.Skill.Effects[i];
                if (effect == null)
                {
                    continue;
                }

                CombatActionResult effectResult = ExecuteSkillEffect(in request, effect);
                if (!effectResult.Success)
                {
                    return effectResult;
                }

                aggregate.DamageDealt += effectResult.DamageDealt;
                aggregate.HealingDone += effectResult.HealingDone;
                aggregate.SummonedCount += effectResult.SummonedCount;
                aggregate.TerrainEdited |= effectResult.TerrainEdited;
                aggregate.HadBuffOrDebuff |= effectResult.HadBuffOrDebuff;
            }

            return aggregate;
        }

        private CombatActionResult ExecuteSkillEffect(in CombatActionRequest request, CombatSkillEffectDefinition effect)
        {
            switch (effect.EffectType)
            {
                case CombatSkillEffectType.Damage:
                    if (request.TargetEntity == null || !request.TargetEntity.IsAlive)
                    {
                    return CombatActionResult.Fail("Damage effect requires a live target.");
                    }

                    float scaledDamage = EvaluateDamage(
                        request.Actor.AttackPower * Mathf.Max(0f, effect.Magnitude),
                        request.TargetEntity.Defense,
                        request.Context);
                    request.TargetEntity.ApplyDamage(scaledDamage);
                    return new CombatActionResult { Success = true, DamageDealt = scaledDamage };

                case CombatSkillEffectType.Heal:
                    CombatEntityRuntime healTarget = request.TargetEntity ?? request.Actor;
                    float healed = healTarget.Heal(Mathf.Max(0f, effect.Magnitude));
                    return new CombatActionResult { Success = true, HealingDone = healed };

                case CombatSkillEffectType.BuffDebuff:
                    return new CombatActionResult { Success = true, HadBuffOrDebuff = true };

                case CombatSkillEffectType.BreakBlock:
                case CombatSkillEffectType.PlaceBlock:
                    return ExecuteTerrainAction(in request, effect.EffectType == CombatSkillEffectType.PlaceBlock);

                case CombatSkillEffectType.Summon:
                    return new CombatActionResult
                    {
                        Success = true,
                        SummonedCount = Mathf.Max(1, effect.Count),
                    };
            }

            return CombatActionResult.Fail("Unsupported skill effect.");
        }

        private CombatActionResult ExecuteTerrainAction(in CombatActionRequest request, bool isPlace)
        {
            if (!request.Context.CanEditTerrain())
            {
                return CombatActionResult.Fail("Terrain authority denied.");
            }

            return new CombatActionResult
            {
                Success = true,
                TerrainEdited = true,
                AnimationActionName = ResolveDefaultAnimationActionName(
                    isPlace ? CombatActionKind.PlaceBlock : CombatActionKind.BreakBlock,
                    request.PreferredAnimationActionName),
            };
        }

        private static string ResolveSkillAnimationActionName(in CombatActionRequest request)
        {
            if (request.Skill != null && !string.IsNullOrWhiteSpace(request.Skill.AnimationActionName))
            {
                return request.Skill.AnimationActionName;
            }

            return ResolveDefaultAnimationActionName(CombatActionKind.Skill, request.PreferredAnimationActionName);
        }

        private static string ResolveDefaultAnimationActionName(CombatActionKind actionKind, string preferred)
        {
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                return preferred;
            }

            return actionKind switch
            {
                CombatActionKind.Attack => "Punch",
                CombatActionKind.Dodge => "RollForward",
                CombatActionKind.BreakBlock => "Punch",
                CombatActionKind.PlaceBlock => "SpecialAttack1",
                CombatActionKind.Skill => "SpecialAttack1",
                _ => null,
            };
        }

        private float EvaluateDamage(float attackPower, float defense, in CombatContext context)
        {
            float attack = Mathf.Max(0f, attackPower) * context.GetDamageMultiplier(m_Config);
            float defenseScaled = Mathf.Max(0f, defense) * context.GetDefenseMultiplier(m_Config);
            return Mathf.Max(1f, attack - defenseScaled);
        }
    }
}
