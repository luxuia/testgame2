using Minecraft.Configurations;
using Minecraft.Combat;
using Minecraft.PlayerControls;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Minecraft.Entities
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BlockInteraction))]
    [RequireComponent(typeof(FluidInteractor))]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerEntity : Entity
    {
        // ...fields of entity class

        [Space]
        [Header("Input")]
        [SerializeField] private InputActionAsset m_InputActions;

        [Space]
        [Header("Config")]
        public float WalkSpeed;
        public float RunSpeed;
        public float FlyUpSpeed;
        public float JumpHeight;
        [SerializeField] private float m_StepInterval;
        [SerializeField] [Range(0, 1)] private float m_RunstepLengthen;

        [Space]
        [Header("Combat Runtime")]
        [SerializeField] [Min(1f)] private float m_CombatMaxHealth = 100f;
        [SerializeField] [Min(0f)] private float m_CombatInitialHealth = 100f;
        [SerializeField] [Min(0f)] private float m_CombatAttackPower = 10f;
        [SerializeField] [Min(0f)] private float m_CombatDefense = 2f;
        [SerializeField] [Min(0f)] private float m_DespawnDelayOnDeath = 1.5f;
        [SerializeField] private bool m_DestroyOnDespawn = true;
        [SerializeField] private bool m_SnapToGroundOnInitialize = true;
        [SerializeField] [Min(0f)] private float m_GroundSnapOffset = 0.05f;

        [Space]

        [SerializeField] private FirstPersonLook m_FirstPersonLook;
        [SerializeField] private FOVKick m_FOVKick;
        [SerializeField] private CurveControlledBob m_HeadBob;
        [SerializeField] private LerpControlledBob m_JumpBob;
        [SerializeField] private FighterAnimatorDriver m_FighterAnimator;
        [SerializeField] private CombatFeedbackView m_CombatFeedback;

        [Space]
        [Header("Events")]
        [SerializeField] private UnityEvent<BlockData> m_OnStepOnBlock;


        [NonSerialized] private InputAction m_MoveAction;
        // [NonSerialized] private InputAction m_RunAction;
        [NonSerialized] private InputAction m_LookAction;
        [NonSerialized] private InputAction m_JumpAction;
        [NonSerialized] private InputAction m_FlyAction;
        [NonSerialized] private InputAction m_FlyDownAction;
        [NonSerialized] private InputAction m_CursorStateAction;

        private Camera m_Camera;
        private Transform m_CameraTransform;
        private FluidInteractor m_FluidInteractor;
        private PlayerController m_PlayerController;
        private BlockInteraction m_BlockInteraction;
        private PathfindingMovementController m_PathfindingMovementController;

        private Vector3 m_OriginalCameraPosition;
        private bool m_Jump;
        private bool m_FlyDown;
        private bool m_IsRunning;
        private bool m_PreviouslyGrounded;
        private bool m_JumpConsumedWhileGrounded;
        private bool m_WasGroundedForAnimation;
        private float m_StepCycle;
        private float m_NextStep;

        private float m_LastTimePressW;
        private Vector3Int m_LastFungalFootprint;
        private float m_NextFungalFootprintTime;
        [NonSerialized] private CombatEntityRuntime m_CombatRuntime;
        [NonSerialized] private float m_DeathEnteredTime = -1f;
        [NonSerialized] private bool m_DeathControlLocked;
        [NonSerialized] private bool m_GroundSnapResolved;

        public CombatEntityRuntime CombatRuntime => m_CombatRuntime;


        protected override void Start()
        {
            base.Start();
            TrySnapToGroundOnInitialize();

            m_InputActions.Enable();
            m_MoveAction = m_InputActions["Player/Move"];
            // m_RunAction = m_InputActions["Player/Run"];
            m_LookAction = m_InputActions["Player/Look"];
            m_JumpAction = m_InputActions["Player/Jump"];
            m_FlyAction = m_InputActions["Player/Fly"];
            m_FlyDownAction = m_InputActions["Player/Fly Down"];
            m_CursorStateAction = m_InputActions["Player/Cursor State"];

            m_Camera = Camera.main;
            m_CameraTransform = m_Camera.GetComponent<Transform>();
            m_FluidInteractor = GetComponent<FluidInteractor>();
            m_PlayerController = GetComponent<PlayerController>();
            m_BlockInteraction = GetComponent<BlockInteraction>();
            m_PathfindingMovementController = GetComponent<PathfindingMovementController>();

            m_FirstPersonLook.Initialize(m_Transform, m_CameraTransform, true);
            m_HeadBob.Initialize(m_CameraTransform);
            m_JumpBob.Initialize();

            m_OriginalCameraPosition = m_CameraTransform.localPosition;
            m_StepCycle = 0f;
            m_NextStep = m_StepCycle * 0.5f;

            m_LastTimePressW = 0f;
            m_LastFungalFootprint = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            m_NextFungalFootprintTime = 0f;

            // m_RunAction.performed += SwitchRunMode;
            m_JumpAction.performed += SwitchJumpMode;
            m_JumpAction.canceled += SwitchJumpMode;
            m_FlyAction.performed += SwitchFlyMode;
            m_FlyDownAction.performed += SwitchFlyDownMode;
            m_CursorStateAction.performed += SwitchCursorState;

            m_BlockInteraction.Initialize(m_Camera, this);
            m_BlockInteraction.enabled = true;

            m_PlayerController.Initialize(m_Camera, this);
            m_PlayerController.enabled = true;

            InitializeCombatRuntimeIfNeeded();
            ResolveFighterAnimatorIfNeeded();
            ResolveCombatFeedbackIfNeeded();
            RefreshCombatFeedbackHealth();
            m_WasGroundedForAnimation = true;
        }

        private void SwitchJumpMode(InputAction.CallbackContext context)
        {
            m_Jump = context.ReadValueAsButton();
        }

        // private void SwitchRunMode(InputAction.CallbackContext context)
        // {
        //     m_IsRunning = true;
        // }

        private void SwitchFlyMode(InputAction.CallbackContext context)
        {
            UseGravity = !UseGravity;
        }

        private void SwitchFlyDownMode(InputAction.CallbackContext context)
        {
            m_FlyDown = context.ReadValueAsButton();
        }

        private void SwitchCursorState(InputAction.CallbackContext context)
        {
            bool value = context.ReadValueAsButton();
            m_FirstPersonLook.SetCursorLockMode(!value);
        }

        private void Update()
        {
            TrySnapToGroundOnInitialize();
            SyncCombatRuntimePosition();
            TickCombatLifecycle();
            RefreshCombatFeedbackHealth();

            if (!CanAcceptControlInput())
            {
                return;
            }

            // 在 Update 里读取输入。
            // 如果在 FixedUpdate 里读输入会出现丢失，
            // 因为 FixedUpdate 不是按实际帧率执行
            SwitchWalkAndRunMode();

            m_FirstPersonLook.LookRotation(m_LookAction.ReadValue<Vector2>(), Time.deltaTime);

            bool isGrounded = GetIsGrounded(out BlockData groundBlock);
            UpdateFighterAnimator(Velocity, isGrounded);

            if (!m_PreviouslyGrounded && isGrounded)
            {
                StartCoroutine(m_JumpBob.DoBobCycle());
                PlayBlockStepSound(groundBlock);

                m_NextStep = m_StepCycle + 0.5f;
            }

            m_PreviouslyGrounded = isGrounded;
        }

        private void TrySnapToGroundOnInitialize()
        {
            if (m_GroundSnapResolved)
            {
                return;
            }

            if (!m_SnapToGroundOnInitialize)
            {
                m_GroundSnapResolved = true;
                return;
            }

            IWorld world = World;
            if (world == null || !world.Initialized || world.RWAccessor == null)
            {
                return;
            }

            Vector3 current = m_Transform.position;
            int x = Mathf.FloorToInt(current.x);
            int z = Mathf.FloorToInt(current.z);
            int topY = world.RWAccessor.GetTopVisibleBlockY(x, z, int.MinValue);
            if (topY == int.MinValue)
            {
                return;
            }

            float feetOffset = Mathf.Max(0f, -BoundingBox.Min.y);
            float desiredY = topY + feetOffset + Mathf.Max(0f, m_GroundSnapOffset);
            if (desiredY < current.y - 0.0001f)
            {
                current.y = desiredY;
                m_Transform.position = current;
            }

            SyncCombatRuntimePosition();
            m_GroundSnapResolved = true;
        }

        protected override void FixedUpdate()
        {
            TickCombatLifecycle();
            if (!CanAcceptControlInput())
            {
                return;
            }

            float speed = GetInput(out Vector2 input);
            m_FluidInteractor.UpdateState(this, m_CameraTransform, out float vMultiplier);
            bool groundedForAnimation = true;

            bool hasPlayerInput = input != Vector2.zero;

            if (hasPlayerInput && m_PlayerController != null && m_PlayerController.IsMoving)
            {
                m_PlayerController.CancelCurrentAction();
            }

            Vector3 velocity;

            if (m_PlayerController != null && m_PlayerController.IsMoving && m_PlayerController.HasDirection && !hasPlayerInput)
            {
                Vector3 moveDir = m_PlayerController.TargetDirection;
                velocity = moveDir * speed * vMultiplier;
                
                if (moveDir.sqrMagnitude > 0.01f)
                {
                    m_Transform.rotation = Quaternion.LookRotation(moveDir.normalized);
                }

                if (UseGravity && m_PlayerController.HasVerticalMovement)
                {
                    bool isGrounded = GetIsGrounded(out BlockData groundBlock);
                    if (isGrounded)
                    {
                        AddInstantForce(new Vector3(0, JumpHeight * Mass / Time.fixedDeltaTime, 0));
                    }
                }
            }
            else
            {
                velocity = m_CameraTransform.forward * input.y + m_CameraTransform.right * input.x;
                velocity = speed * vMultiplier * velocity.normalized;

                if (input != Vector2.zero)
                {
                    Vector3 moveDir = m_CameraTransform.forward * input.y + m_CameraTransform.right * input.x;
                    moveDir.y = 0;
                    if (moveDir.sqrMagnitude > 0.01f)
                    {
                        m_Transform.rotation = Quaternion.LookRotation(moveDir.normalized);
                    }
                }
            }

            if (UseGravity)
            {
                velocity.y = Velocity.y;

                bool isGrounded = GetIsGrounded(out BlockData groundBlock);
                groundedForAnimation = isGrounded;

                if (!m_Jump)
                {
                    m_JumpConsumedWhileGrounded = false;
                }

                if (isGrounded && m_Jump && !m_JumpConsumedWhileGrounded)
                {
                    AddInstantForce(new Vector3(0, JumpHeight * Mass / Time.fixedDeltaTime, 0));
                    PlayBlockStepSound(groundBlock);
                    m_FighterAnimator?.PlayAction(Velocity.sqrMagnitude > 0.2f
                        ? FighterAnimationAction.JumpForward
                        : FighterAnimationAction.Jump);
                    m_JumpConsumedWhileGrounded = true;
                }

                ProgressStepCycle(input, speed, isGrounded, groundBlock);
            }
            else if (m_FlyDown)
            {
                velocity.y = -FlyUpSpeed;
            }
            else if (m_Jump)
            {
                velocity.y = FlyUpSpeed;
            }
            else
            {
                velocity.y = 0;
            }

            AddInstantForce((velocity - Velocity) * Mass / Time.fixedDeltaTime);

            base.FixedUpdate();
            bool groundedAfterMove = UseGravity ? GetIsGrounded(out _) : true;
            UpdateFighterAnimator(Velocity, groundedAfterMove);
            TrySpreadFungalCarpet(velocity);
        }

        private void ProgressStepCycle(Vector2 input, float speed, bool isGrounded, BlockData blockUnderFeet)
        {
            Vector3 velocity = Velocity;

            if (velocity.sqrMagnitude > 0 && input != Vector2.zero)
            {
                m_StepCycle += (Velocity.magnitude + (speed * (m_IsRunning ? m_RunstepLengthen : 1f))) * Time.fixedDeltaTime;
            }

            if (m_StepCycle <= m_NextStep)
            {
                return;
            }

            m_NextStep = m_StepCycle + m_StepInterval;

            if (isGrounded)
            {
                PlayBlockStepSound(blockUnderFeet);
            }
        }

        private void UpdateCameraPosition(float speed, bool isGrounded)
        {
            Vector3 newCameraPosition;

            if (!m_HeadBob.Enabled)
            {
                return;
            }

            if (Velocity.sqrMagnitude > 0 && isGrounded)
            {
                m_CameraTransform.localPosition = m_HeadBob.DoHeadBob(Velocity.magnitude + (speed * (m_IsRunning ? m_RunstepLengthen : 1f)), m_StepInterval, Time.fixedDeltaTime);
                newCameraPosition = m_CameraTransform.localPosition;
                newCameraPosition.y = m_CameraTransform.localPosition.y - m_JumpBob.GetOffset();
            }
            else
            {
                newCameraPosition = m_CameraTransform.localPosition;
                newCameraPosition.y = m_OriginalCameraPosition.y - m_JumpBob.GetOffset();
            }

            m_CameraTransform.localPosition = newCameraPosition;
        }

        private void SwitchWalkAndRunMode()
        {
            // TODO: 如何用 Input System 实现这个功能？
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                float currentTime = Time.time;

                if (currentTime - m_LastTimePressW <= 0.2f)
                {
                    // 此时认为玩家双击了 W 或者 向上箭头
                    m_IsRunning = true;
                }

                m_LastTimePressW = currentTime;
            }
        }

        private float GetInput(out Vector2 input)
        {
            input = m_MoveAction.ReadValue<Vector2>();

            if (input == Vector2.zero)
            {
                // 一旦玩家停止移动，结束跑步状态
                m_IsRunning = false;
            }

            return m_IsRunning ? RunSpeed : WalkSpeed;
        }

        private void PlayBlockStepSound(BlockData block)
        {
            //block.PlayStepAutio(m_AudioSource);
        }

        private void TrySpreadFungalCarpet(Vector3 desiredVelocity)
        {
            if (Time.time < m_NextFungalFootprintTime)
            {
                return;
            }

            Vector2 planar = new Vector2(desiredVelocity.x, desiredVelocity.z);
            if (planar.sqrMagnitude < 0.01f)
            {
                return;
            }

            int x = Mathf.FloorToInt(Position.x);
            int z = Mathf.FloorToInt(Position.z);
            if (x == m_LastFungalFootprint.x && z == m_LastFungalFootprint.z)
            {
                return;
            }

            if (!Minecraft.FungalCarpetSystem.TryInfectAtWorldPosition(Position))
            {
                return;
            }

            m_LastFungalFootprint = new Vector3Int(x, 0, z);
            m_NextFungalFootprintTime = Time.time + 0.08f;
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

        private void InitializeCombatRuntimeIfNeeded()
        {
            if (m_CombatRuntime != null)
            {
                BindCombatRuntimeEvents();
                return;
            }

            float maxHealth = Mathf.Max(1f, m_CombatMaxHealth);
            float initialHealth = Mathf.Clamp(m_CombatInitialHealth, 0f, maxHealth);

            m_CombatRuntime = new CombatEntityRuntime
            {
                RuntimeId = GetInstanceID(),
                Role = CombatActorRole.Player,
                DisplayName = gameObject.name,
                MaxHealth = maxHealth,
                CurrentHealth = initialHealth,
                AttackPower = Mathf.Max(0f, m_CombatAttackPower),
                Defense = Mathf.Max(0f, m_CombatDefense),
                Position = Position,
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

            m_CombatRuntime.Position = Position;
        }

        private bool CanAcceptControlInput()
        {
            return m_CombatRuntime == null || m_CombatRuntime.CanAct;
        }

        private void TickCombatLifecycle()
        {
            if (m_CombatRuntime == null || m_CombatRuntime.LifecycleState != CombatLifecycleState.Dead || m_DeathEnteredTime < 0f)
            {
                return;
            }

            if (Time.time >= m_DeathEnteredTime + Mathf.Max(0f, m_DespawnDelayOnDeath))
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
            LockControlsOnDeath();
            m_FighterAnimator?.PlayAction(FighterAnimationAction.Death);
            RefreshCombatFeedbackHealth();
        }

        private void HandleRuntimeDespawn(CombatEntityRuntime runtime)
        {
            if (runtime != m_CombatRuntime)
            {
                return;
            }

            if (m_DestroyOnDespawn)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void LockControlsOnDeath()
        {
            if (m_DeathControlLocked)
            {
                return;
            }

            m_DeathControlLocked = true;
            m_Jump = false;
            m_FlyDown = false;
            m_IsRunning = false;

            if (m_PlayerController != null)
            {
                m_PlayerController.CancelCurrentAction();
                m_PlayerController.enabled = false;
            }

            if (m_PathfindingMovementController != null)
            {
                m_PathfindingMovementController.ClearTarget();
                m_PathfindingMovementController.enabled = false;
            }

            if (m_BlockInteraction != null)
            {
                m_BlockInteraction.enabled = false;
            }

            if (m_InputActions != null)
            {
                m_InputActions.Disable();
            }
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
            m_CombatFeedback?.SetHealth(m_CombatRuntime.CurrentHealth, m_CombatRuntime.MaxHealth);
        }

        private void UpdateFighterAnimator(Vector3 worldVelocity, bool isGrounded)
        {
            if (m_FighterAnimator == null)
            {
                return;
            }

            m_FighterAnimator.UpdateLocomotion(worldVelocity, m_CameraTransform, m_IsRunning);
            m_FighterAnimator.SetGroundedState(isGrounded || !UseGravity);

            if (m_WasGroundedForAnimation && !isGrounded && worldVelocity.y > 0.25f)
            {
                m_FighterAnimator.PlayAction(worldVelocity.sqrMagnitude > 0.2f
                    ? FighterAnimationAction.JumpForward
                    : FighterAnimationAction.Jump);
            }

            m_WasGroundedForAnimation = isGrounded;
        }

        private void OnDestroy()
        {
            if (m_JumpAction != null)
            {
                m_JumpAction.performed -= SwitchJumpMode;
                m_JumpAction.canceled -= SwitchJumpMode;
            }

            if (m_FlyAction != null)
            {
                m_FlyAction.performed -= SwitchFlyMode;
            }

            if (m_FlyDownAction != null)
            {
                m_FlyDownAction.performed -= SwitchFlyDownMode;
            }

            if (m_CursorStateAction != null)
            {
                m_CursorStateAction.performed -= SwitchCursorState;
            }

            UnbindCombatRuntimeEvents();
        }
    }
}
