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
            ResolveSlotsIfNeeded();
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

        private void ResolveSlotsIfNeeded()
        {
            if (!m_PanelBody)
            {
                Transform body = Transform.Find("PanelBody");
                if (body)
                {
                    m_PanelBody = body.gameObject;
                }
            }

            if (m_TabSlots == null || m_TabSlots.Length == 0)
            {
                string[] names = { "Tab_Build", "Tab_Slime", "Tab_Adjutant", "Tab_Resource", "Tab_Quest", "Tab_Diplomacy" };
                m_TabSlots = new GameObject[names.Length];
                Transform body = m_PanelBody ? m_PanelBody.transform : Transform.Find("PanelBody");

                for (int i = 0; i < names.Length; i++)
                {
                    Transform tab = body ? body.Find(names[i]) : null;
                    m_TabSlots[i] = tab ? tab.gameObject : null;
                }
            }
        }
    }
}
