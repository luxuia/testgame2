using System;
using System.Collections.Generic;
using UnityEngine;

namespace Minecraft.Entities
{
    public enum FighterAnimationAction : byte
    {
        DashForward = 0,
        DashBackward = 1,
        DashLeft = 2,
        DashRight = 3,
        Intro1 = 4,
        Intro2 = 5,
        Victory1 = 6,
        Victory2 = 7,
        Uppercut = 8,
        Punch = 9,
        HeavySmash = 10,
        SmashCombo = 11,
        Combo1 = 12,
        ForwardSmash = 13,
        Jab = 14,
        Kick = 15,
        AxeKick = 16,
        BlockHitReact = 17,
        BlockBreak = 18,
        CrouchBlockHitReact = 19,
        LightHit = 20,
        Knockdown = 21,
        Choke = 22,
        LowKick = 23,
        Sweep = 24,
        DownSmash = 25,
        LowPunch = 26,
        Jump = 27,
        JumpForward = 28,
        JumpBackward = 29,
        HighPunch = 30,
        HighSmash = 31,
        JumpHitReact = 32,
        HighKick = 33,
        RollForward = 34,
        RollBackward = 35,
        RangeAttack1 = 36,
        RangeAttack2 = 37,
        MoveAttack1 = 38,
        MoveAttack2 = 39,
        SpecialAttack1 = 40,
        SpecialAttack2 = 41,
        Death = 42,
        Revive = 43,
    }

    [DisallowMultipleComponent]
    public class FighterAnimatorDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator m_Animator;

        [Header("Simple Controller")]
        [SerializeField] [Min(0.01f)] private float m_CrossFadeDuration = 0.08f;
        [SerializeField] [Min(0.01f)] private float m_MinMoveSpeed = 0.05f;
        [SerializeField] [Min(0f)] private float m_MinActionInterval = 0.06f;
        [SerializeField] [Min(0f)] private float m_LocomotionResumeDelay = 0.18f;

        [Header("State Names")]
        [SerializeField] private string m_BaseLayerName = "Base Layer";
        [SerializeField] private string m_HitLayerName = "hit";
        [SerializeField] private string m_IdleStateName = "Idle";
        [SerializeField] private string m_RunStateName = "Run";
        [SerializeField] private string m_LightHitStateName = "LightHit";
        [SerializeField] private string m_BlockHitStateName = "BlockHitReact";
        [SerializeField] private string m_CrouchBlockHitStateName = "BlockHitReact";
        [SerializeField] private string m_JumpHitStateName = "Stunned";

        [Header("Hit Layer")]
        [SerializeField] [Min(0f)] private float m_HitActionCooldown = 0.5f;
        [SerializeField] [Range(0f, 1f)] private float m_HitLayerPlayWeight = 1f;
        [SerializeField] [Min(0.01f)] private float m_HitLayerActiveDuration = 0.32f;
        [SerializeField] [Min(0.01f)] private float m_HitLayerWeightLerpSpeed = 10f;

        private static readonly Dictionary<string, FighterAnimationAction> s_NameToAction = BuildActionLookup();
        private static readonly HashSet<FighterAnimationAction> s_HitActions = new HashSet<FighterAnimationAction>
        {
            FighterAnimationAction.LightHit,
            FighterAnimationAction.BlockHitReact,
            FighterAnimationAction.CrouchBlockHitReact,
            FighterAnimationAction.JumpHitReact,
        };

        private float m_LastActionTime = -999f;
        private float m_LastHitActionTime = -999f;
        private float m_LocomotionLockedUntil = -999f;
        private bool m_Blocking;
        private bool m_Crouching;
        private bool m_Dead;
        private bool m_IsGrounded = true;
        private int m_BaseLayerIndex = -1;
        private int m_HitLayerIndex = -1;
        private string m_CurrentBaseState;
        private readonly HashSet<string> m_ReportedMissingStates = new HashSet<string>(StringComparer.Ordinal);
        private float m_HitLayerActiveUntil = -999f;
        private float m_CurrentHitLayerWeight;

        public Animator Animator => m_Animator;
        public bool IsDead => m_Dead;

        private void Awake()
        {
            ResolveAnimatorIfNeeded();
            SyncStanceBools();
        }

        private void LateUpdate()
        {
            TickHitLayerWeight();
        }

        public bool TryInitialize(Animator explicitAnimator = null)
        {
            if (explicitAnimator != null)
            {
                m_Animator = explicitAnimator;
            }

            if (!ResolveAnimatorIfNeeded())
            {
                return false;
            }

            ResolveLayerIndices();
            return true;
        }

        public void SetBlock(bool value)
        {
            m_Blocking = value;
        }

        public void SetCrouch(bool value)
        {
            m_Crouching = value;
        }

        public void SetGroundedState(bool isGrounded)
        {
            m_IsGrounded = isGrounded;
        }

        public void UpdateLocomotion(Vector3 worldVelocity, Transform reference, bool runRequested)
        {
            if (!ResolveAnimatorIfNeeded())
            {
                return;
            }

            if (m_Dead)
            {
                return;
            }

            _ = reference;
            _ = runRequested; // runRequested is intentionally ignored in simple crossfade mode.

            if (Time.time < m_LocomotionLockedUntil)
            {
                return;
            }

            if (m_Blocking || m_Crouching)
            {
                TryCrossFadeBaseState(m_IdleStateName);
                return;
            }

            if (!m_IsGrounded)
            {
                return;
            }

            Vector3 planarVelocity = worldVelocity;
            planarVelocity.y = 0f;
            float speed = planarVelocity.magnitude;
            string desiredState = speed > m_MinMoveSpeed ? m_RunStateName : m_IdleStateName;
            TryCrossFadeBaseState(desiredState);
        }

        public bool PlayAction(FighterAnimationAction action)
        {
            if (!ResolveAnimatorIfNeeded())
            {
                return false;
            }

            if (Time.time - m_LastActionTime < m_MinActionInterval)
            {
                return false;
            }

            if (s_HitActions.Contains(action))
            {
                return TryPlayHitAction(action);
            }

            string stateName = ResolveActionStateName(action);
            if (string.IsNullOrWhiteSpace(stateName))
            {
                Debug.LogError($"[FighterAnimatorDriver] Empty mapped state for action '{action}' on '{name}'.", this);
                return false;
            }

            if (TryCrossFadeBaseState(stateName, force: true))
            {
                m_LastActionTime = Time.time;
                if (action == FighterAnimationAction.Death)
                {
                    m_Dead = true;
                    m_LocomotionLockedUntil = float.MaxValue;
                }
                else if (action == FighterAnimationAction.Revive)
                {
                    m_Dead = false;
                    m_LocomotionLockedUntil = Time.time + Mathf.Max(0f, m_LocomotionResumeDelay);
                }
                else
                {
                    m_LocomotionLockedUntil = Time.time + Mathf.Max(0f, m_LocomotionResumeDelay);
                }

                return true;
            }

            Debug.LogError($"[FighterAnimatorDriver] Missing base state '{stateName}' for action '{action}' on '{name}'.", this);
            return false;
        }

        public bool PlayActionByName(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                Debug.LogError($"[FighterAnimatorDriver] Empty action name on '{name}'.", this);
                return false;
            }

            string normalized = NormalizeActionName(actionName);
            if (!s_NameToAction.TryGetValue(normalized, out FighterAnimationAction action))
            {
                Debug.LogError($"[FighterAnimatorDriver] Unknown action name '{actionName}' on '{name}'.", this);
                return false;
            }

            return PlayAction(action);
        }

        private bool TryPlayHitAction(FighterAnimationAction action)
        {
            if (Time.time - m_LastHitActionTime < Mathf.Max(0f, m_HitActionCooldown))
            {
                return false;
            }

            string stateName = ResolveHitStateName(action);
            if (string.IsNullOrWhiteSpace(stateName))
            {
                Debug.LogError($"[FighterAnimatorDriver] Empty hit state for action '{action}' on '{name}'.", this);
                return false;
            }

            if (!TryCrossFadeHitState(stateName))
            {
                Debug.LogError($"[FighterAnimatorDriver] Missing hit state '{stateName}' for action '{action}' on '{name}'.", this);
                return false;
            }

            m_LastHitActionTime = Time.time;
            m_LastActionTime = Time.time;
            m_HitLayerActiveUntil = Time.time + Mathf.Max(0.01f, m_HitLayerActiveDuration);
            TickHitLayerWeight(force: true);
            return true;
        }

        private string ResolveHitStateName(FighterAnimationAction action)
        {
            return action switch
            {
                FighterAnimationAction.LightHit => m_LightHitStateName,
                FighterAnimationAction.BlockHitReact => m_BlockHitStateName,
                FighterAnimationAction.CrouchBlockHitReact => m_CrouchBlockHitStateName,
                FighterAnimationAction.JumpHitReact => m_JumpHitStateName,
                _ => m_BlockHitStateName,
            };
        }

        private bool TryCrossFadeBaseState(string stateName, bool force = false)
        {
            if (string.IsNullOrWhiteSpace(stateName) || m_BaseLayerIndex < 0)
            {
                return false;
            }

            if (!force &&
                !string.IsNullOrEmpty(m_CurrentBaseState) &&
                string.Equals(m_CurrentBaseState, stateName, StringComparison.Ordinal))
            {
                return true;
            }

            if (!TryCrossFadeState(m_BaseLayerIndex, stateName))
            {
                return false;
            }

            m_CurrentBaseState = stateName;
            return true;
        }

        private bool TryCrossFadeHitState(string stateName)
        {
            if (m_HitLayerIndex < 0)
            {
                Debug.LogError($"[FighterAnimatorDriver] Missing hit layer '{m_HitLayerName}' on '{name}'.", this);
                return false;
            }

            return TryCrossFadeState(m_HitLayerIndex, stateName);
        }

        private bool TryCrossFadeState(int layerIndex, string stateName)
        {
            if (m_Animator == null || layerIndex < 0 || string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            string layerName = layerIndex == m_BaseLayerIndex ? m_BaseLayerName : m_HitLayerName;
            string fullPath = $"{layerName}.{stateName}";
            int fullPathHash = Animator.StringToHash(fullPath);
            if (!m_Animator.HasState(layerIndex, fullPathHash))
            {
                ReportMissingStateOnce(layerName, stateName, fullPath);
                return false;
            }

            m_Animator.CrossFadeInFixedTime(fullPathHash, Mathf.Max(0.01f, m_CrossFadeDuration), layerIndex, 0f);
            return true;
        }

        private bool ResolveAnimatorIfNeeded()
        {
            if (m_Animator != null)
            {
                return true;
            }

            m_Animator = GetComponent<Animator>();
            if (m_Animator == null)
            {
                m_Animator = GetComponentInChildren<Animator>();
            }

            return m_Animator != null;
        }

        private void ResolveLayerIndices()
        {
            if (m_Animator == null)
            {
                m_BaseLayerIndex = -1;
                m_HitLayerIndex = -1;
                return;
            }

            m_BaseLayerIndex = m_Animator.GetLayerIndex(m_BaseLayerName);
            if (m_BaseLayerIndex < 0)
            {
                m_BaseLayerIndex = 0;
                Debug.LogError($"[FighterAnimatorDriver] Base layer '{m_BaseLayerName}' not found on '{name}'. Fallback to layer 0.", this);
            }

            m_HitLayerIndex = m_Animator.GetLayerIndex(m_HitLayerName);

            m_Animator.SetLayerWeight(m_BaseLayerIndex, 1f);
            if (m_HitLayerIndex >= 0)
            {
                m_CurrentHitLayerWeight = 0f;
                m_Animator.SetLayerWeight(m_HitLayerIndex, 0f);
            }
        }

        private void SyncStanceBools()
        {
            _ = m_Blocking;
            _ = m_Crouching;
        }

        private static Dictionary<string, FighterAnimationAction> BuildActionLookup()
        {
            var result = new Dictionary<string, FighterAnimationAction>(StringComparer.OrdinalIgnoreCase);
            foreach (FighterAnimationAction action in Enum.GetValues(typeof(FighterAnimationAction)))
            {
                string name = action.ToString();
                result[NormalizeActionName(name)] = action;
            }

            return result;
        }

        private static string NormalizeActionName(string input)
        {
            return input.Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .Trim()
                .ToLowerInvariant();
        }

        private static string ResolveActionStateName(FighterAnimationAction action)
        {
            return action.ToString();
        }

        private void ReportMissingStateOnce(string layerName, string stateName, string fullPath)
        {
            string key = $"{layerName}:{stateName}";
            if (m_ReportedMissingStates.Add(key))
            {
                Debug.LogError(
                    $"[FighterAnimatorDriver] Animator state '{fullPath}' not found on '{name}'.",
                    this);
            }
        }

        private void TickHitLayerWeight(bool force = false)
        {
            if (m_Animator == null || m_HitLayerIndex < 0)
            {
                return;
            }

            float targetWeight = Time.time <= m_HitLayerActiveUntil
                ? Mathf.Clamp01(m_HitLayerPlayWeight)
                : 0f;
            float lerpSpeed = Mathf.Max(0.01f, m_HitLayerWeightLerpSpeed);
            float nextWeight = force
                ? targetWeight
                : Mathf.MoveTowards(m_CurrentHitLayerWeight, targetWeight, lerpSpeed * Time.deltaTime);

            if (!force && Mathf.Abs(nextWeight - m_CurrentHitLayerWeight) <= 0.0001f)
            {
                return;
            }

            m_CurrentHitLayerWeight = nextWeight;
            m_Animator.SetLayerWeight(m_HitLayerIndex, m_CurrentHitLayerWeight);
        }
    }
}
