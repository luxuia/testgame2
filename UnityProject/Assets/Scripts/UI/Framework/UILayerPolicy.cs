using UnityEngine;

namespace Minecraft.UI.Framework
{
    [DisallowMultipleComponent]
    public sealed class UILayerPolicy : MonoBehaviour
    {
        [SerializeField] private Transform m_HUDLayer;
        [SerializeField] private Transform m_SidePanelLayer;
        [SerializeField] private Transform m_CombatOverlayLayer;
        [SerializeField] private Transform m_ModalLayer;
        [SerializeField] private CanvasGroup m_HUDBlockerGroup;
        [SerializeField] private CanvasGroup m_SidePanelBlockerGroup;
        [SerializeField] private bool m_BlockLowerLayersWhenModalActive = true;

        private int m_ModalOpenCount;

        public Transform HUDLayer => m_HUDLayer;
        public Transform SidePanelLayer => m_SidePanelLayer;
        public Transform CombatOverlayLayer => m_CombatOverlayLayer;
        public Transform ModalLayer => m_ModalLayer;

        private void Awake()
        {
            ApplyLayerOrder();
            ApplyBlockingState();
        }

        public void ApplyLayerOrder()
        {
            if (m_HUDLayer) m_HUDLayer.SetSiblingIndex(0);
            if (m_SidePanelLayer) m_SidePanelLayer.SetSiblingIndex(1);
            if (m_CombatOverlayLayer) m_CombatOverlayLayer.SetSiblingIndex(2);
            if (m_ModalLayer) m_ModalLayer.SetSiblingIndex(3);
        }

        public void NotifyModalOpened()
        {
            m_ModalOpenCount++;
            ApplyBlockingState();
        }

        public void NotifyModalClosed()
        {
            if (m_ModalOpenCount > 0)
            {
                m_ModalOpenCount--;
            }

            ApplyBlockingState();
        }

        private void ApplyBlockingState()
        {
            bool blockLower = m_BlockLowerLayersWhenModalActive && m_ModalOpenCount > 0;
            SetBlocksRaycasts(m_HUDBlockerGroup, !blockLower);
            SetBlocksRaycasts(m_SidePanelBlockerGroup, !blockLower);
        }

        private static void SetBlocksRaycasts(CanvasGroup group, bool blocksRaycasts)
        {
            if (!group)
            {
                return;
            }

            group.blocksRaycasts = blocksRaycasts;
        }
    }
}
