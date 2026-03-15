using UnityEngine;

namespace Minecraft.UI.Framework
{
    [DisallowMultipleComponent]
    public sealed class UICommandRouter : MonoBehaviour
    {
        [SerializeField] private UIRuntimeRegistry m_RuntimeRegistry;
        [SerializeField] private KeyCode m_ToggleSidePanelKey = KeyCode.E;
        [SerializeField] private KeyCode m_ToggleModalKey = KeyCode.Escape;
        [SerializeField] private KeyCode m_ToggleCombatOverlayKey = KeyCode.Tab;

        private void Awake()
        {
            if (!m_RuntimeRegistry)
            {
                m_RuntimeRegistry = GetComponent<UIRuntimeRegistry>();
            }
        }

        private void Update()
        {
            if (!m_RuntimeRegistry)
            {
                return;
            }

            if (Input.GetKeyDown(m_ToggleSidePanelKey))
            {
                m_RuntimeRegistry.OpenOrNavigate(UIPrefabKeys.SidePanel);
                if (m_RuntimeRegistry.TryGetView(UIPrefabKeys.SidePanel, out SidePanelView sidePanel))
                {
                    sidePanel.ToggleCollapsed();
                }
            }

            if (Input.GetKeyDown(m_ToggleModalKey))
            {
                m_RuntimeRegistry.OpenOrNavigate(UIPrefabKeys.ModalTemplate);
            }

            if (Input.GetKeyDown(m_ToggleCombatOverlayKey))
            {
                m_RuntimeRegistry.OpenOrNavigate(UIPrefabKeys.CombatOverlay);
                if (m_RuntimeRegistry.TryGetView(UIPrefabKeys.CombatOverlay, out CombatOverlayView combatOverlay))
                {
                    combatOverlay.SetCombatVisible(true);
                }
            }
        }
    }
}
