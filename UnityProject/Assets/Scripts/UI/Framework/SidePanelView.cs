using ToaruUnity.UI;
using UnityEngine;

namespace Minecraft.UI.Framework
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class SidePanelView : AbstractUGUIView
    {
        [SerializeField] private GameObject m_PanelBody;
        [SerializeField] private GameObject[] m_TabSlots;

        private bool m_IsCollapsed;

        public bool IsCollapsed => m_IsCollapsed;
        public GameObject[] TabSlots => m_TabSlots;

        protected override void OnCreate()
        {
            base.OnCreate();
            ApplyCollapsedState(m_IsCollapsed);
        }

        public void SetCollapsed(bool collapsed)
        {
            m_IsCollapsed = collapsed;
            ApplyCollapsedState(m_IsCollapsed);
        }

        public void ToggleCollapsed()
        {
            SetCollapsed(!m_IsCollapsed);
        }

        private void ApplyCollapsedState(bool collapsed)
        {
            if (m_PanelBody)
            {
                m_PanelBody.SetActive(!collapsed);
            }
        }
    }
}
