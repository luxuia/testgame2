using ToaruUnity.UI;
using UnityEngine;

namespace Minecraft.UI.Framework
{
    [DisallowMultipleComponent]
    public sealed class UIBootstrap : MonoBehaviour
    {
        [SerializeField] private GameObject m_UIRootPrefab;
        [SerializeField] private Transform m_RuntimeParent;
        [SerializeField] private bool m_OpenDefaultViewsOnStart = true;

        private static GameObject s_RuntimeRoot;

        private UIManager m_UIManager;

        private void Awake()
        {
            EnsureRoot();
        }

        private void Start()
        {
            if (!m_OpenDefaultViewsOnStart || !m_UIManager)
            {
                return;
            }

            m_UIManager.OpenNewView(UIPrefabKeys.HUDRoot);
            m_UIManager.OpenNewView(UIPrefabKeys.SidePanel);
            m_UIManager.OpenNewView(UIPrefabKeys.CombatOverlay);
        }

        private void EnsureRoot()
        {
            if (s_RuntimeRoot)
            {
                m_UIManager = s_RuntimeRoot.GetComponentInChildren<UIManager>(true);
                return;
            }

            if (!m_UIRootPrefab)
            {
                Debug.LogWarning("UIBootstrap missing UIRoot prefab.");
                return;
            }

            s_RuntimeRoot = Instantiate(m_UIRootPrefab, m_RuntimeParent ? m_RuntimeParent : transform);
            s_RuntimeRoot.name = m_UIRootPrefab.name;
            m_UIManager = s_RuntimeRoot.GetComponentInChildren<UIManager>(true);
        }
    }
}
