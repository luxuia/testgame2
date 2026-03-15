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

        [Space]

        [SerializeField] private FirstPersonLook m_FirstPersonLook;
        [SerializeField] private FOVKick m_FOVKick;
        [SerializeField] private CurveControlledBob m_HeadBob;
        [SerializeField] private LerpControlledBob m_JumpBob;
        [SerializeField] private FighterAnimatorDriver m_FighterAnimator;

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

        public CombatEntityRuntime CombatRuntime => m_CombatRuntime;


        protected override void Start()
        {
            base.Start();

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

            BlockInteraction interaction = GetComponent<BlockInteraction>();
            interaction.Initialize(m_Camera, this);
            interaction.enabled = true;

            m_PlayerController.Initialize(m_Camera, this);
            m_PlayerController.enabled = true;

            InitializeCombatRuntimeIfNeeded();
            ResolveFighterAnimatorIfNeeded();
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
            SyncCombatRuntimePosition();

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

        protected override void FixedUpdate()
        {
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

        private void InitializeCombatRuntimeIfNeeded()
        {
            if (m_CombatRuntime != null)
            {
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
            };
        }

        private void SyncCombatRuntimePosition()
        {
            if (m_CombatRuntime == null)
            {
                return;
            }

            m_CombatRuntime.Position = Position;
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
    }
}
