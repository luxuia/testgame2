using Minecraft.Combat;
using Minecraft.Entities;
using UnityEngine;

namespace Minecraft.Pathfinding
{
    [DisallowMultipleComponent]
    public class CrowdAgentController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("水平移动速度")]
        public float MoveSpeed = 4f;

        [Tooltip("竖直移动速度")]
        public float VerticalMoveSpeed = 6f;

        [Tooltip("朝向插值速度")]
        public float RotationSpeed = 10f;

        [Tooltip("枢轴到脚底的高度")]
        public float PivotHeightFromFeet = 1f;

        [Tooltip("节点判定距离")]
        public float NodeReachDistance = 0.08f;

        [Tooltip("竖直到位判定距离")]
        public float VerticalReachDistance = 0.08f;

        [Tooltip("上行台阶时，只有水平接近该距离内才开始抬升")]
        public float UpwardMoveStartDistance = 0.45f;

        [Header("Fungal Carpet")]
        [Tooltip("Agent 移动时菌毯足迹刷新间隔（秒）")]
        public float FungalFootprintInterval = 0.12f;

        [Header("Combat Runtime")]
        public CombatActorRole CombatRole = CombatActorRole.Puji;
        [Min(1f)] public float CombatMaxHealth = 60f;
        [Min(0f)] public float CombatInitialHealth = 60f;
        [Min(0f)] public float CombatAttackPower = 6f;
        [Min(0f)] public float CombatDefense = 1f;
        [SerializeField] private FighterAnimatorDriver m_FighterAnimator;

        [System.NonSerialized] private Vector3Int? m_TargetBlock;
        [System.NonSerialized] private Vector2Int m_LastFungalFootprintXZ = new Vector2Int(int.MinValue, int.MinValue);
        [System.NonSerialized] private float m_NextFungalFootprintTime;
        [System.NonSerialized] private CombatEntityRuntime m_CombatRuntime;
        [System.NonSerialized] private bool m_WasGroundedForAnimation = true;

        public bool HasTarget => m_TargetBlock.HasValue;
        public Vector3Int TargetBlock => m_TargetBlock ?? default;
        public CombatEntityRuntime CombatRuntime => m_CombatRuntime;

        private void Awake()
        {
            InitializeCombatRuntimeIfNeeded();
            SyncCombatRuntimePosition();
            ResolveFighterAnimatorIfNeeded();
        }

        public void SetTarget(Vector3Int target) => m_TargetBlock = target;

        public void ClearTarget() => m_TargetBlock = null;

        public Vector3Int GetCurrentGridPosition()
        {
            Vector3 pos = transform.position;
            float feetY = pos.y - PivotHeightFromFeet + 0.01f;
            return new Vector3Int(
                Mathf.FloorToInt(pos.x),
                Mathf.FloorToInt(feetY),
                Mathf.FloorToInt(pos.z));
        }

        public void MoveTowardsNode(Vector3Int nextNode, float deltaTime)
        {
            Vector2 desired = GetDesiredPlanarVelocity(nextNode);
            MoveWithPlanarVelocity(nextNode, new Vector3(desired.x, 0f, desired.y), deltaTime);
        }

        public Vector2 GetDesiredPlanarVelocity(Vector3Int nextNode)
        {
            Vector3 targetPos = new Vector3(
                nextNode.x + 0.5f,
                nextNode.y + PivotHeightFromFeet,
                nextNode.z + 0.5f);

            Vector3 toTarget = targetPos - transform.position;
            Vector2 planar = new Vector2(toTarget.x, toTarget.z);
            float planarDistance = planar.magnitude;
            if (planarDistance <= NodeReachDistance || planarDistance <= 0.0001f)
            {
                return Vector2.zero;
            }

            return planar / planarDistance * MoveSpeed;
        }

        public bool IsNearNode(Vector3Int nextNode)
        {
            Vector3 targetPos = new Vector3(
                nextNode.x + 0.5f,
                nextNode.y + PivotHeightFromFeet,
                nextNode.z + 0.5f);
            Vector3 current = transform.position;
            float dx = targetPos.x - current.x;
            float dz = targetPos.z - current.z;
            float dy = Mathf.Abs(targetPos.y - current.y);
            return (dx * dx + dz * dz) <= NodeReachDistance * NodeReachDistance &&
                   dy <= Mathf.Max(0.01f, VerticalReachDistance);
        }

        public float GetPlanarDistanceToNode(Vector3Int nextNode)
        {
            Vector3 targetPos = new Vector3(
                nextNode.x + 0.5f,
                nextNode.y + PivotHeightFromFeet,
                nextNode.z + 0.5f);
            Vector3 current = transform.position;
            float dx = targetPos.x - current.x;
            float dz = targetPos.z - current.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public void MoveWithPlanarVelocity(Vector3Int nextNode, Vector3 planarVelocity, float deltaTime)
        {
            Vector3 targetPos = new Vector3(
                nextNode.x + 0.5f,
                nextNode.y + PivotHeightFromFeet,
                nextNode.z + 0.5f);

            Vector3 current = transform.position;
            Vector3 toTarget = targetPos - current;

            Vector3 planar = new Vector3(toTarget.x, 0f, toTarget.z);
            float planarDistance = planar.magnitude;

            if (planarDistance > NodeReachDistance)
            {
                Vector3 planarMove = planarVelocity * deltaTime;
                if (planarMove.magnitude > planarDistance)
                {
                    planarMove = planar;
                }

                current += planarMove;
            }
            else
            {
                current.x = targetPos.x;
                current.z = targetPos.z;
            }

            float yTarget = targetPos.y;
            if (targetPos.y > current.y + 0.001f)
            {
                float startUpDistance = Mathf.Max(NodeReachDistance * 2f, UpwardMoveStartDistance);
                if (planarDistance > startUpDistance)
                {
                    yTarget = current.y;
                }
            }

            current.y = Mathf.MoveTowards(current.y, yTarget, VerticalMoveSpeed * deltaTime);
            transform.position = current;

            Vector3 forward = planarVelocity.sqrMagnitude > 0.0001f ? planarVelocity : planar;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, RotationSpeed * deltaTime);
            }

            SyncCombatRuntimePosition();
            UpdateFighterAnimator(planarVelocity, Mathf.Abs(targetPos.y - current.y) <= Mathf.Max(0.01f, VerticalReachDistance));
            TrySpreadFungalCarpet(planarVelocity);
        }

        public float ApplyCombatDamage(float amount)
        {
            InitializeCombatRuntimeIfNeeded();
            float before = m_CombatRuntime.CurrentHealth;
            float applied = m_CombatRuntime.ApplyDamage(amount);
            if (applied > 0f && m_FighterAnimator != null)
            {
                if (before > 0f && m_CombatRuntime.CurrentHealth <= 0f)
                {
                    m_FighterAnimator.PlayAction(FighterAnimationAction.Death);
                }
                else
                {
                    m_FighterAnimator.PlayAction(FighterAnimationAction.LightHit);
                }
            }

            return applied;
        }

        public float HealCombat(float amount)
        {
            InitializeCombatRuntimeIfNeeded();
            float before = m_CombatRuntime.CurrentHealth;
            float healed = m_CombatRuntime.Heal(amount);
            if (healed > 0f && before <= 0f && m_CombatRuntime.CurrentHealth > 0f)
            {
                m_FighterAnimator?.PlayAction(FighterAnimationAction.Revive);
            }

            return healed;
        }

        private void TrySpreadFungalCarpet(Vector3 planarVelocity)
        {
            if (Time.time < m_NextFungalFootprintTime)
            {
                return;
            }

            Vector2 planar = new Vector2(planarVelocity.x, planarVelocity.z);
            if (planar.sqrMagnitude < 0.01f)
            {
                return;
            }

            int x = Mathf.FloorToInt(transform.position.x);
            int z = Mathf.FloorToInt(transform.position.z);
            Vector2Int current = new Vector2Int(x, z);

            if (current == m_LastFungalFootprintXZ)
            {
                return;
            }

            if (!Minecraft.FungalCarpetSystem.TryInfectAtWorldPosition(transform.position))
            {
                return;
            }

            m_LastFungalFootprintXZ = current;
            m_NextFungalFootprintTime = Time.time + Mathf.Max(0.02f, FungalFootprintInterval);
        }

        private void InitializeCombatRuntimeIfNeeded()
        {
            if (m_CombatRuntime != null)
            {
                return;
            }

            float maxHealth = Mathf.Max(1f, CombatMaxHealth);
            float initialHealth = Mathf.Clamp(CombatInitialHealth, 0f, maxHealth);

            m_CombatRuntime = new CombatEntityRuntime
            {
                RuntimeId = GetInstanceID(),
                Role = CombatRole,
                DisplayName = gameObject.name,
                MaxHealth = maxHealth,
                CurrentHealth = initialHealth,
                AttackPower = Mathf.Max(0f, CombatAttackPower),
                Defense = Mathf.Max(0f, CombatDefense),
                Position = transform.position,
            };
        }

        private void SyncCombatRuntimePosition()
        {
            if (m_CombatRuntime == null)
            {
                return;
            }

            m_CombatRuntime.Position = transform.position;
        }

        private void ResolveFighterAnimatorIfNeeded()
        {
            if (m_FighterAnimator == null)
            {
                m_FighterAnimator = GetComponent<FighterAnimatorDriver>();
            }

            if (m_FighterAnimator == null)
            {
                m_FighterAnimator = GetComponentInChildren<FighterAnimatorDriver>();
            }

            if (m_FighterAnimator == null)
            {
                Animator foundAnimator = GetComponentInChildren<Animator>();
                if (foundAnimator != null)
                {
                    m_FighterAnimator = foundAnimator.GetComponent<FighterAnimatorDriver>();
                    if (m_FighterAnimator == null)
                    {
                        m_FighterAnimator = foundAnimator.gameObject.AddComponent<FighterAnimatorDriver>();
                    }
                }
            }

            m_FighterAnimator?.TryInitialize();
        }

        private void UpdateFighterAnimator(Vector3 planarVelocity, bool isGrounded)
        {
            if (m_FighterAnimator == null)
            {
                return;
            }

            bool runRequested = planarVelocity.sqrMagnitude >= (MoveSpeed * MoveSpeed * 0.75f);
            m_FighterAnimator.UpdateLocomotion(planarVelocity, transform, runRequested);
            m_FighterAnimator.SetGroundedState(isGrounded);

            if (m_WasGroundedForAnimation && !isGrounded)
            {
                m_FighterAnimator.PlayAction(planarVelocity.sqrMagnitude > 0.05f
                    ? FighterAnimationAction.JumpForward
                    : FighterAnimationAction.Jump);
            }

            m_WasGroundedForAnimation = isGrounded;
        }
    }
}
