using System.Collections.Generic;
using Minecraft.UI;
using UnityEngine;

namespace Minecraft.PlayerControls
{
    /// <summary>
    /// 统一采样输入并产出单帧命令，避免多个系统重复轮询 Input。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class PlayerCommandRouter : MonoBehaviour
    {
        [Header("UI Command Keys")]
        [SerializeField] private KeyCode m_ToggleSidePanelKey = KeyCode.E;
        [SerializeField] private KeyCode m_ToggleModalKey = KeyCode.Escape;
        [SerializeField] private KeyCode m_ToggleCombatOverlayKey = KeyCode.Tab;

        [Header("Gameplay Command Keys")]
        [SerializeField] private KeyCode m_CancelSelectionKey = KeyCode.Escape;
        [SerializeField] private KeyCode m_ForwardPrimaryKey = KeyCode.W;
        [SerializeField] private KeyCode m_ForwardSecondaryKey = KeyCode.UpArrow;

        private readonly List<PlayerCommand> m_FrameCommands = new List<PlayerCommand>(16);
        private int m_SampledFrame = -1;

        public static PlayerCommandRouter Instance { get; private set; }

        public IReadOnlyList<PlayerCommand> FrameCommands
        {
            get
            {
                SampleFrameIfNeeded();
                return m_FrameCommands;
            }
        }

        public static PlayerCommandRouter Resolve(MonoBehaviour context)
        {
            if (context != null)
            {
                var localRouter = context.GetComponent<PlayerCommandRouter>();
                if (localRouter != null)
                {
                    return localRouter;
                }
            }

            if (Instance)
            {
                return Instance;
            }

            Instance = FindAnyObjectByType<PlayerCommandRouter>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            SampleFrameIfNeeded();
        }

        private void SampleFrameIfNeeded()
        {
            int frame = Time.frameCount;
            if (m_SampledFrame == frame)
            {
                return;
            }

            // 同一帧只采样一次，保证 UI/玩法消费的是同一份输入快照。
            m_SampledFrame = frame;
            m_FrameCommands.Clear();

            Vector2 pointerPosition = ResolvePointerPosition();
            AddPointerCommandIf(Input.GetMouseButtonDown(0), PlayerCommandType.MovePrimaryDown, pointerPosition);
            AddPointerCommandIf(Input.GetMouseButtonDown(1), PlayerCommandType.MineSecondaryDown, pointerPosition);
            AddPointerCommandIf(Input.GetMouseButton(1), PlayerCommandType.MineSecondaryHold, pointerPosition);
            AddPointerCommandIf(Input.GetMouseButtonUp(1), PlayerCommandType.MineSecondaryUp, pointerPosition);

            AddKeyCommandIf(Input.GetKeyDown(m_CancelSelectionKey), PlayerCommandType.CancelSelection);
            AddKeyCommandIf(Input.GetKeyDown(m_ToggleSidePanelKey), PlayerCommandType.ToggleSidePanel);
            AddKeyCommandIf(Input.GetKeyDown(m_ToggleModalKey), PlayerCommandType.ToggleModal);
            AddKeyCommandIf(Input.GetKeyDown(m_ToggleCombatOverlayKey), PlayerCommandType.ToggleCombatOverlay);
            AddKeyCommandIf(Input.GetKeyDown(m_ForwardPrimaryKey) || Input.GetKeyDown(m_ForwardSecondaryKey), PlayerCommandType.ForwardTap);
        }

        private void AddPointerCommandIf(bool condition, PlayerCommandType type, Vector2 pointerPosition)
        {
            if (!condition)
            {
                return;
            }

            m_FrameCommands.Add(PlayerCommand.CreatePointer(type, pointerPosition));
        }

        private void AddKeyCommandIf(bool condition, PlayerCommandType type)
        {
            if (!condition)
            {
                return;
            }

            m_FrameCommands.Add(PlayerCommand.Create(type));
        }

        private static Vector2 ResolvePointerPosition()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                return CursorReticle.MousePos + new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            return Input.mousePosition;
        }
    }
}
