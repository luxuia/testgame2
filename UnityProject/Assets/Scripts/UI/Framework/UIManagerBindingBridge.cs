using System.Reflection;
using ToaruUnity.UI;
using UnityEngine;

namespace Minecraft.UI.Framework
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-2000)]
    public sealed class UIManagerBindingBridge : MonoBehaviour
    {
        private static readonly FieldInfo s_ViewContainerField =
            typeof(UIManager).GetField("m_ViewContainer", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo s_ViewLoaderField =
            typeof(UIManager).GetField("m_ViewLoader", BindingFlags.Instance | BindingFlags.NonPublic);

        [SerializeField] private UIManager m_UIManager;
        [SerializeField] private UIPrefabViewLoader m_ViewLoader;
        [SerializeField] private Transform m_DefaultContainer;

        private void Awake()
        {
            if (!m_UIManager)
            {
                m_UIManager = GetComponent<UIManager>();
            }

            if (!m_ViewLoader)
            {
                m_ViewLoader = GetComponent<UIPrefabViewLoader>();
            }

            if (!m_DefaultContainer)
            {
                m_DefaultContainer = transform;
            }

            if (!m_UIManager || s_ViewContainerField == null || s_ViewLoaderField == null)
            {
                return;
            }

            if (s_ViewContainerField.GetValue(m_UIManager) == null)
            {
                s_ViewContainerField.SetValue(m_UIManager, m_DefaultContainer);
            }

            if (s_ViewLoaderField.GetValue(m_UIManager) == null && m_ViewLoader)
            {
                s_ViewLoaderField.SetValue(m_UIManager, m_ViewLoader);
            }
        }
    }
}
