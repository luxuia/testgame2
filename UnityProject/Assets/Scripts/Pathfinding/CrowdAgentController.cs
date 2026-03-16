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
        [Tooltip("下行台阶时，只有水平接近该距离内才开始下降（防止跨楼层提前下坠）")]
        public float DownwardMoveStartDistance = 0.45f;

        [Header("Fungal Carpet")]
        [Tooltip("Agent 移动时菌毯足迹刷新间隔（秒）")]
        public float FungalFootprintInterval = 0.12f;

        [Header("Spawn Grounding")]
        [Tooltip("初始化时自动向下贴地（不会向上抬升）")]
        public bool SnapToGroundOnInitialize = true;
        [Tooltip("贴地时给脚底预留的微小离地偏移")]
        [Min(0f)] public float GroundSnapOffset = 0.05f;
        [Tooltip("初始化贴地允许的最大下落高度，超过该值视为高低层结构并跳过贴地")]
        [Min(0f)] public float MaxGroundSnapDropDistance = 1.25f;

        [Header("Combat Runtime")]
        public CombatActorRole CombatRole = CombatActorRole.Puji;
        [Min(1f)] public float CombatMaxHealth = 60f;
        [Min(0f)] public float CombatInitialHealth = 60f;
        [Min(0f)] public float CombatAttackPower = 6f;
        [Min(0f)] public float CombatDefense = 1f;
        [Min(0f)] public float DespawnDelayOnDeath = 1.2f;
        public bool DestroyOnDespawn = true;
        [SerializeField] private FighterAnimatorDriver m_FighterAnimator;
        [SerializeField] private CombatFeedbackView m_CombatFeedback;

        [System.NonSerialized] private Vector3Int? m_TargetBlock;
        [System.NonSerialized] private Vector2Int m_LastFungalFootprintXZ = new Vector2Int(int.MinValue, int.MinValue);
        [System.NonSerialized] private float m_NextFungalFootprintTime;
        [System.NonSerialized] private CombatEntityRuntime m_CombatRuntime;
        [System.NonSerialized] private bool m_WasGroundedForAnimation = true;
        [System.NonSerialized] private float m_DeathEnteredTime = -1f;
        [System.NonSerialized] private bool m_MovementLockedByDeath;
        [System.NonSerialized] private bool m_GroundSnapResolved;

        public bool HasTarget => m_TargetBlock.HasValue;
        public Vector3Int TargetBlock => m_TargetBlock ?? default;
        public CombatEntityRuntime CombatRuntime => m_CombatRuntime;

        private void Awake()
        {
            InitializeCombatRuntimeIfNeeded();
            SyncCombatRuntimePosition();
            ResolveFighterAnimatorIfNeeded();
            ResolveCombatFeedbackIfNeeded();
            RefreshCombatFeedbackHealth();
        }

        private void Update()
        {
            TrySnapToGroundOnInitialize();
            SyncCombatRuntimePosition();
            TickCombatLifecycle();
            RefreshCombatFeedbackHealth();
        }

        private void OnDestroy()
        {
            UnbindCombatRuntimeEvents();
        }

        public void SetTarget(Vector3Int target)
        {
            if (!CanNavigate())
            {
                return;
            }

            m_TargetBlock = target;
        }

        public void ClearTarget()
        {
            m_TargetBlock = null;
            ForceIdleAnimation();
        }

        public Vector3Int GetCurrentGridPosition()
        {
            Vector3 pos = transform.position;
            Vector3Int probe = new Vector3Int(
                Mathf.FloorToInt(pos.x),
                Mathf.FloorToInt(pos.y),
                Mathf.FloorToInt(pos.z));

            IWorld world = World.Active;
            if (world != null && world.Initialized)
            {
                if (AStarPathfinding.IsWalkableNode(probe, world))
                {
                    return probe;
                }

                Vector3Int? walkable = AStarPathfinding.FindNearestWalkableNode(probe, world, 2);
                if (walkable.HasValue)
                {
                    return walkable.Value;
                }
            }

            // Fallback only when world/query is unavailable.
            float feetY = pos.y - PivotHeightFromFeet + 0.2f;
            return new Vector3Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(feetY), Mathf.FloorToInt(pos.z));
        }

        public void MoveTowardsNode(Vector3Int nextNode, float deltaTime)
        {
            if (!CanNavigate())
            {
                ForceIdleAnimation();
                return;
            }

            Vector2 desired = GetDesiredPlanarVelocity(nextNode);
            MoveWithPlanarVelocity(nextNode, new Vector3(desired.x, 0f, desired.y), deltaTime);
        }

        public Vector2 GetDesiredPlanarVelocity(Vector3Int nextNode)
        {
            if (!CanNavigate())
            {
                return Vector2.zero;
            }

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
            if (!CanNavigate())
            {
                ForceIdleAnimation();
                return;
            }

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
            else if (targetPos.y < current.y - 0.001f)
            {
                float startDownDistance = Mathf.Max(NodeReachDistance * 2f, DownwardMoveStartDistance);
                if (planarDistance > startDownDistance)
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
            return m_CombatRuntime.TryApplyHit(amount, out float applied, out _) ? applied : 0f;
        }

        public float HealCombat(float amount)
        {
            InitializeCombatRuntimeIfNeeded();
            return m_CombatRuntime.Heal(amount);
        }

        public void ForceIdleAnimation(bool isGrounded = true)
        {
            ResolveFighterAnimatorIfNeeded();
            if (m_FighterAnimator == null)
            {
                return;
            }

            m_FighterAnimator.UpdateLocomotion(Vector3.zero, transform, false);
            m_FighterAnimator.SetGroundedState(isGrounded);
        }

        public bool PlayActionAnimation(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName) || m_CombatRuntime == null || !m_CombatRuntime.CanAct)
            {
                return false;
            }

            ResolveFighterAnimatorIfNeeded();
            if (m_FighterAnimator == null)
            {
                return false;
            }

            // Force locomotion booleans to idle before firing one-shot action triggers.
            // Some animator controllers only allow attacks from idle-like sub states.
            m_FighterAnimator.UpdateLocomotion(Vector3.zero, transform, false);
            m_FighterAnimator.SetGroundedState(m_WasGroundedForAnimation);

            if (m_FighterAnimator.PlayActionByName(actionName))
            {
                return true;
            }

            Debug.LogError($"[CrowdAgentController] Failed to play action '{actionName}' on '{name}'.", this);
            return false;
        }

        private bool CanNavigate()
        {
            return !m_MovementLockedByDeath && (m_CombatRuntime == null || m_CombatRuntime.CanAct);
        }

        private void TickCombatLifecycle()
        {
            if (m_CombatRuntime == null || m_CombatRuntime.LifecycleState != CombatLifecycleState.Dead || m_DeathEnteredTime < 0f)
            {
                return;
            }

            if (Time.time >= m_DeathEnteredTime + Mathf.Max(0f, DespawnDelayOnDeath))
            {
                m_CombatRuntime.TryMarkDespawned();
            }
        }

        private void BindCombatRuntimeEvents()
        {
            if (m_CombatRuntime == null)
            {
                return;
            }

            m_CombatRuntime.OnHit -= HandleRuntimeHit;
            m_CombatRuntime.OnDamageApplied -= HandleRuntimeDamageApplied;
            m_CombatRuntime.OnDeath -= HandleRuntimeDeath;
            m_CombatRuntime.OnDespawn -= HandleRuntimeDespawn;

            m_CombatRuntime.OnHit += HandleRuntimeHit;
            m_CombatRuntime.OnDamageApplied += HandleRuntimeDamageApplied;
            m_CombatRuntime.OnDeath += HandleRuntimeDeath;
            m_CombatRuntime.OnDespawn += HandleRuntimeDespawn;
        }

        private void UnbindCombatRuntimeEvents()
        {
            if (m_CombatRuntime == null)
            {
                return;
            }

            m_CombatRuntime.OnHit -= HandleRuntimeHit;
            m_CombatRuntime.OnDamageApplied -= HandleRuntimeDamageApplied;
            m_CombatRuntime.OnDeath -= HandleRuntimeDeath;
            m_CombatRuntime.OnDespawn -= HandleRuntimeDespawn;
        }

        private void HandleRuntimeHit(CombatEntityRuntime runtime, float incomingDamage)
        {
            _ = runtime;
            _ = incomingDamage;
        }

        private void HandleRuntimeDamageApplied(CombatEntityRuntime runtime, float damage)
        {
            if (runtime != m_CombatRuntime)
            {
                return;
            }

            ResolveFighterAnimatorIfNeeded();
            if (damage > 0f &&
                runtime.LifecycleState == CombatLifecycleState.Alive &&
                runtime.CurrentHealth > 0f &&
                m_FighterAnimator != null)
            {
                m_FighterAnimator.PlayAction(FighterAnimationAction.LightHit);
                ResolveCombatFeedbackIfNeeded();
                m_CombatFeedback?.ShowDamage(damage);
            }

            RefreshCombatFeedbackHealth();
        }

        private void HandleRuntimeDeath(CombatEntityRuntime runtime)
        {
            if (runtime != m_CombatRuntime)
            {
                return;
            }

            m_DeathEnteredTime = Time.time;
            m_MovementLockedByDeath = true;
            ClearTarget();
            m_FighterAnimator?.PlayAction(FighterAnimationAction.Death);
            RefreshCombatFeedbackHealth();
        }

        private void HandleRuntimeDespawn(CombatEntityRuntime runtime)
        {
            if (runtime != m_CombatRuntime)
            {
                return;
            }

            ClearTarget();
            if (DestroyOnDespawn)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
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
                BindCombatRuntimeEvents();
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
                LifecycleState = initialHealth > 0f ? CombatLifecycleState.Alive : CombatLifecycleState.Dead,
            };

            BindCombatRuntimeEvents();
            if (initialHealth <= 0f)
            {
                m_CombatRuntime.EnterDeathLifecycle();
            }

            ResolveCombatFeedbackIfNeeded();
            RefreshCombatFeedbackHealth();
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

        private void ResolveCombatFeedbackIfNeeded()
        {
            if (m_CombatFeedback == null)
            {
                m_CombatFeedback = GetComponent<CombatFeedbackView>();
            }

            if (m_CombatFeedback == null)
            {
                m_CombatFeedback = gameObject.AddComponent<CombatFeedbackView>();
            }

            m_CombatFeedback?.EnsureInitialized();
        }

        private void RefreshCombatFeedbackHealth()
        {
            if (m_CombatRuntime == null)
            {
                return;
            }

            ResolveCombatFeedbackIfNeeded();
            m_CombatFeedback?.SetOverlayMeta(m_CombatRuntime.DisplayName);
            m_CombatFeedback?.SetHealth(m_CombatRuntime.CurrentHealth, m_CombatRuntime.MaxHealth);
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
                m_FighterAnimator.PlayAction(FighterAnimationAction.Jump);
            }

            m_WasGroundedForAnimation = isGrounded;
        }

        private void TrySnapToGroundOnInitialize()
        {
            if (m_GroundSnapResolved)
            {
                return;
            }

            if (!SnapToGroundOnInitialize)
            {
                m_GroundSnapResolved = true;
                return;
            }

            IWorld world = World.Active;
            if (world == null || !world.Initialized || world.RWAccessor == null)
            {
                return;
            }

            Vector3 current = transform.position;
            int x = Mathf.FloorToInt(current.x);
            int z = Mathf.FloorToInt(current.z);
            int topY = world.RWAccessor.GetTopVisibleBlockY(x, z, int.MinValue);
            if (topY == int.MinValue)
            {
                return;
            }

            float desiredY = topY + Mathf.Max(0f, PivotHeightFromFeet) + Mathf.Max(0f, GroundSnapOffset);
            float dropDistance = current.y - desiredY;
            if (dropDistance > Mathf.Max(0f, MaxGroundSnapDropDistance))
            {
                m_GroundSnapResolved = true;
                return;
            }

            if (desiredY < current.y - 0.0001f)
            {
                current.y = desiredY;
                transform.position = current;
            }

            SyncCombatRuntimePosition();
            m_GroundSnapResolved = true;
        }
    }
}
