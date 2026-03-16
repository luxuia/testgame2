using System;
using System.Collections;
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

        [Header("Locomotion Tuning")]
        [SerializeField] [Min(0.01f)] private float m_MinMoveSpeed = 0.05f;
        [SerializeField] [Range(0f, 1f)] private float m_StrafeThreshold = 0.2f;
        [SerializeField] [Range(0f, 1f)] private float m_ForwardThreshold = 0.25f;

        [Header("Action Timing")]
        [SerializeField] [Min(0f)] private float m_MinActionInterval = 0.06f;
        [SerializeField] [Min(0f)] private float m_InAirDelay = 0.25f;
        [SerializeField] [Min(0.01f)] private float m_InAirDuration = 0.5f;

        [Header("Animator Parameters")]
        [SerializeField] private string m_RunParam = "Run";
        [SerializeField] private string m_WalkForwardParam = "Walk Forward";
        [SerializeField] private string m_WalkBackwardParam = "Walk Backward";
        [SerializeField] private string m_WalkLeftParam = "WalkLeft";
        [SerializeField] private string m_WalkRightParam = "WalkRight";
        [SerializeField] private string m_WalkSlowParam = "WalkSlow";
        [SerializeField] private string m_BlockParam = "Block";
        [SerializeField] private string m_CrouchParam = "Crouch";
        [SerializeField] private string m_InAirParam = "InAir";

        private static readonly Dictionary<FighterAnimationAction, string> s_ActionTriggers = new Dictionary<FighterAnimationAction, string>
        {
            { FighterAnimationAction.DashForward, "DashForwardTrigger" },
            { FighterAnimationAction.DashBackward, "DashBackwardTrigger" },
            { FighterAnimationAction.DashLeft, "DashLeftTrigger" },
            { FighterAnimationAction.DashRight, "DashRightTrigger" },
            { FighterAnimationAction.Intro1, "Intro1Trigger" },
            { FighterAnimationAction.Intro2, "Intro2Trigger" },
            { FighterAnimationAction.Victory1, "Victory1Trigger" },
            { FighterAnimationAction.Victory2, "Victory2Trigger" },
            { FighterAnimationAction.Uppercut, "UppercutTrigger" },
            { FighterAnimationAction.Punch, "PunchTrigger" },
            { FighterAnimationAction.HeavySmash, "HeavySmashTrigger" },
            { FighterAnimationAction.SmashCombo, "SmashComboTrigger" },
            { FighterAnimationAction.Combo1, "Combo1Trigger" },
            { FighterAnimationAction.ForwardSmash, "ForwardSmashTrigger" },
            { FighterAnimationAction.Jab, "JabTrigger" },
            { FighterAnimationAction.Kick, "KickTrigger" },
            { FighterAnimationAction.AxeKick, "AxeKickTrigger" },
            { FighterAnimationAction.BlockHitReact, "BlockHitReactTrigger" },
            { FighterAnimationAction.BlockBreak, "BlockBreakTrigger" },
            { FighterAnimationAction.CrouchBlockHitReact, "CrouchBlockHitReactTrigger" },
            { FighterAnimationAction.LightHit, "LightHitTrigger" },
            { FighterAnimationAction.Knockdown, "KnockdownTrigger" },
            { FighterAnimationAction.Choke, "Choke" },
            { FighterAnimationAction.LowKick, "LowKickTrigger" },
            { FighterAnimationAction.Sweep, "SweepTrigger" },
            { FighterAnimationAction.DownSmash, "DownSmashTrigger" },
            { FighterAnimationAction.LowPunch, "LowPunchTrigger" },
            { FighterAnimationAction.Jump, "JumpTrigger" },
            { FighterAnimationAction.JumpForward, "JumpForwardTrigger" },
            { FighterAnimationAction.JumpBackward, "JumpBackwardTrigger" },
            { FighterAnimationAction.HighPunch, "HighPunchTrigger" },
            { FighterAnimationAction.HighSmash, "HighSmashTrigger" },
            { FighterAnimationAction.JumpHitReact, "JumpHitReactTrigger" },
            { FighterAnimationAction.HighKick, "HighKickTrigger" },
            { FighterAnimationAction.RollForward, "RollForwardTrigger" },
            { FighterAnimationAction.RollBackward, "RollBackwardTrigger" },
            { FighterAnimationAction.RangeAttack1, "RangeAttack1Trigger" },
            { FighterAnimationAction.RangeAttack2, "RangeAttack2Trigger" },
            { FighterAnimationAction.MoveAttack1, "MoveAttack1Trigger" },
            { FighterAnimationAction.MoveAttack2, "MoveAttack2Trigger" },
            { FighterAnimationAction.SpecialAttack1, "SpecialAttack1Trigger" },
            { FighterAnimationAction.SpecialAttack2, "SpecialAttack2Trigger" },
            { FighterAnimationAction.Death, "DeathTrigger" },
            { FighterAnimationAction.Revive, "ReviveTrigger" },
        };

        private static readonly Dictionary<string, FighterAnimationAction> s_NameToAction = BuildActionLookup();
        private static readonly Dictionary<FighterAnimationAction, string[]> s_ImmediateStatePaths = new Dictionary<FighterAnimationAction, string[]>
        {
            {
                FighterAnimationAction.Jump,
                new[]
                {
                    "Base Layer.Jumps.Jump",
                    "Base Layer.InAir.Jump",
                    "Base Layer.Jump",
                }
            },
            {
                FighterAnimationAction.JumpForward,
                new[]
                {
                    "Base Layer.Jumps.JumpForward",
                    "Base Layer.InAir.JumpForward",
                    "Base Layer.JumpForward",
                }
            },
            {
                FighterAnimationAction.JumpBackward,
                new[]
                {
                    "Base Layer.Jumps.JumpBackward",
                    "Base Layer.InAir.JumpBackward",
                    "Base Layer.JumpBackward",
                }
            },
        };

        private float m_LastActionTime = -999f;
        private bool m_Blocking;
        private bool m_Crouching;
        private bool m_Dead;
        private bool m_InAir;
        private Coroutine m_InAirCoroutine;

        public Animator Animator => m_Animator;
        public bool IsDead => m_Dead;

        private void Awake()
        {
            ResolveAnimatorIfNeeded();
            SyncStanceBools();
        }

        public bool TryInitialize(Animator explicitAnimator = null)
        {
            if (explicitAnimator != null)
            {
                m_Animator = explicitAnimator;
            }

            return ResolveAnimatorIfNeeded();
        }

        public void SetBlock(bool value)
        {
            m_Blocking = value;
            if (!string.IsNullOrEmpty(m_BlockParam) && m_Animator != null)
            {
                m_Animator.SetBool(m_BlockParam, m_Blocking);
            }
        }

        public void SetCrouch(bool value)
        {
            m_Crouching = value;
            if (!string.IsNullOrEmpty(m_CrouchParam) && m_Animator != null)
            {
                m_Animator.SetBool(m_CrouchParam, m_Crouching);
            }
        }

        public void SetGroundedState(bool isGrounded)
        {
            SetInAir(!isGrounded);
        }

        public void UpdateLocomotion(Vector3 worldVelocity, Transform reference, bool runRequested)
        {
            if (!ResolveAnimatorIfNeeded())
            {
                return;
            }

            if (m_Dead)
            {
                SetLocomotion(false, false, false, false, false, false);
                return;
            }

            _ = reference;
            _ = runRequested;

            Vector3 planarVelocity = worldVelocity;
            planarVelocity.y = 0f;
            float speed = planarVelocity.magnitude;
            bool moving = speed > m_MinMoveSpeed && !m_Blocking && !m_Crouching;

            bool run = false;
            bool walkForward = false;
            bool walkBackward = false;
            bool walkLeft = false;
            bool walkRight = false;
            bool walkSlow = false;

            if (moving)
            {
                Vector3 localDirection = transform.InverseTransformDirection(planarVelocity / speed);
                bool directionMismatched = localDirection.z < 1f - m_ForwardThreshold;

                if (!directionMismatched)
                {
                    run = true;
                }
                else
                {
                    float absX = Mathf.Abs(localDirection.x);
                    float absZ = Mathf.Abs(localDirection.z);

                    if (absX < m_StrafeThreshold && absZ < m_ForwardThreshold)
                    {
                        walkSlow = true;
                    }
                    else if (absZ >= absX)
                    {
                        walkForward = localDirection.z >= 0f;
                        walkBackward = !walkForward;
                    }
                    else
                    {
                        walkRight = localDirection.x >= 0f;
                        walkLeft = !walkRight;
                    }
                }
            }

            SetLocomotion(run, walkForward, walkBackward, walkLeft, walkRight, walkSlow);
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

            if (!s_ActionTriggers.TryGetValue(action, out string triggerName) || string.IsNullOrEmpty(triggerName))
            {
                return false;
            }

            if (IsJumpAction(action))
            {
                // Clear locomotion booleans first so jump can interrupt locomotion immediately.
                SetLocomotion(false, false, false, false, false, false);
                if (TryCrossFadeToActionState(action))
                {
                    m_LastActionTime = Time.time;
                    StartInAirWindow();
                    return true;
                }
            }

            m_Animator.SetTrigger(triggerName);
            m_LastActionTime = Time.time;

            if (action == FighterAnimationAction.Death)
            {
                m_Dead = true;
            }
            else if (action == FighterAnimationAction.Revive)
            {
                m_Dead = false;
            }

            if (action == FighterAnimationAction.Jump ||
                action == FighterAnimationAction.JumpForward ||
                action == FighterAnimationAction.JumpBackward)
            {
                StartInAirWindow();
            }

            if (m_Dead)
            {
                SetLocomotion(false, false, false, false, false, false);
            }

            return true;
        }

        public bool PlayActionByName(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            string normalized = NormalizeActionName(actionName);
            if (!s_NameToAction.TryGetValue(normalized, out FighterAnimationAction action))
            {
                return false;
            }

            return PlayAction(action);
        }

        private static bool IsJumpAction(FighterAnimationAction action)
        {
            return action == FighterAnimationAction.Jump ||
                   action == FighterAnimationAction.JumpForward ||
                   action == FighterAnimationAction.JumpBackward;
        }

        private bool TryCrossFadeToActionState(FighterAnimationAction action)
        {
            if (m_Animator == null ||
                !s_ImmediateStatePaths.TryGetValue(action, out string[] statePaths) ||
                statePaths == null)
            {
                return false;
            }

            for (int i = 0; i < statePaths.Length; i++)
            {
                string statePath = statePaths[i];
                if (string.IsNullOrEmpty(statePath))
                {
                    continue;
                }

                int fullPathHash = Animator.StringToHash(statePath);
                if (!m_Animator.HasState(0, fullPathHash))
                {
                    continue;
                }

                m_Animator.CrossFadeInFixedTime(fullPathHash, 0.03f, 0, 0f);
                return true;
            }

            return false;
        }

        private void StartInAirWindow()
        {
            if (m_InAirCoroutine != null)
            {
                StopCoroutine(m_InAirCoroutine);
            }

            m_InAirCoroutine = StartCoroutine(CoInAirWindow());
        }

        private IEnumerator CoInAirWindow()
        {
            if (m_InAirDelay > 0f)
            {
                yield return new WaitForSeconds(m_InAirDelay);
            }

            SetInAir(true);

            if (m_InAirDuration > 0f)
            {
                yield return new WaitForSeconds(m_InAirDuration);
            }

            SetInAir(false);
            m_InAirCoroutine = null;
        }

        private void SetInAir(bool value)
        {
            m_InAir = value;
            if (!string.IsNullOrEmpty(m_InAirParam) && m_Animator != null)
            {
                m_Animator.SetBool(m_InAirParam, m_InAir);
            }
        }

        private void SetLocomotion(bool run, bool walkForward, bool walkBackward, bool walkLeft, bool walkRight, bool walkSlow)
        {
            SetBool(m_RunParam, run);
            SetBool(m_WalkForwardParam, walkForward);
            SetBool(m_WalkBackwardParam, walkBackward);
            SetBool(m_WalkLeftParam, walkLeft);
            SetBool(m_WalkRightParam, walkRight);
            SetBool(m_WalkSlowParam, walkSlow);
        }

        private void SetBool(string param, bool value)
        {
            if (string.IsNullOrEmpty(param) || m_Animator == null)
            {
                return;
            }

            m_Animator.SetBool(param, value);
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

        private void SyncStanceBools()
        {
            SetBlock(m_Blocking);
            SetCrouch(m_Crouching);
            SetInAir(m_InAir);
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
    }
}
