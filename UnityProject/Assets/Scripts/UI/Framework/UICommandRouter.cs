using UnityEngine;
using Minecraft.PlayerControls;

namespace Minecraft.UI.Framework
{
    /// <summary>
    /// 仅负责把规范化输入命令映射为 UI 行为。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UICommandRouter : MonoBehaviour
    {
        [SerializeField] private UIRuntimeRegistry m_RuntimeRegistry;
        [SerializeField] private PlayerCommandRouter m_CommandRouter;

        private void Awake()
        {
            EnsureDependencies();
        }

        private void Update()
        {
            EnsureDependencies();
            if (!m_RuntimeRegistry || !m_CommandRouter)
            {
                return;
            }

            var commands = m_CommandRouter.FrameCommands;
            for (int i = 0; i < commands.Count; i++)
            {
                HandleCommand(commands[i].Type);
            }
        }

        private void EnsureDependencies()
        {
            if (!m_RuntimeRegistry)
            {
                m_RuntimeRegistry = GetComponent<UIRuntimeRegistry>();
            }

            if (!m_CommandRouter)
            {
                m_CommandRouter = GetComponent<PlayerCommandRouter>();
            }

            if (!m_CommandRouter)
            {
                TryResolveCommandRouter();
            }
        }

        private void TryResolveCommandRouter()
        {
            m_CommandRouter = PlayerCommandRouter.Resolve(this);
        }

        private void HandleCommand(PlayerCommandType commandType)
        {
            switch (commandType)
            {
                case PlayerCommandType.ToggleSidePanel:
                    m_RuntimeRegistry.OpenOrNavigate(UIPrefabKeys.SidePanel);
                    if (m_RuntimeRegistry.TryGetView(UIPrefabKeys.SidePanel, out SidePanelView sidePanel))
                    {
                        sidePanel.ToggleCollapsed();
                    }
                    break;
                case PlayerCommandType.ToggleModal:
                    m_RuntimeRegistry.OpenOrNavigate(UIPrefabKeys.ModalTemplate);
                    break;
                case PlayerCommandType.ToggleCombatOverlay:
                    m_RuntimeRegistry.OpenOrNavigate(UIPrefabKeys.CombatOverlay);
                    if (m_RuntimeRegistry.TryGetView(UIPrefabKeys.CombatOverlay, out CombatOverlayView combatOverlay))
                    {
                        combatOverlay.SetCombatVisible(true);
                    }
                    break;
            }
        }
    }
}
