using System;
using System.Collections.Generic;
using ToaruUnity.UI;
using UnityEngine;

namespace Minecraft.UI.Framework
{
    [DisallowMultipleComponent]
    public sealed class UIRuntimeRegistry : MonoBehaviour
    {
        [SerializeField] private UIManager m_UIManager;
        [SerializeField] private UILayerPolicy m_LayerPolicy;

        private readonly Dictionary<object, AbstractView> m_Views = new Dictionary<object, AbstractView>();

        public UIManager UIManager => m_UIManager;

        public bool TryGetView<T>(object key, out T view) where T : AbstractView
        {
            if (m_Views.TryGetValue(key, out AbstractView value) && value is T typed)
            {
                view = typed;
                return true;
            }

            view = null;
            return false;
        }

        public void OpenOrNavigate(object key)
        {
            if (!m_UIManager || key == null)
            {
                return;
            }

            if (m_Views.ContainsKey(key))
            {
                m_UIManager.NavigateToView(key);
            }
            else
            {
                m_UIManager.OpenNewView(key);
            }
        }

        private void Awake()
        {
            if (!m_UIManager)
            {
                m_UIManager = GetComponentInChildren<UIManager>(true);
            }

            if (!m_LayerPolicy)
            {
                m_LayerPolicy = GetComponentInChildren<UILayerPolicy>(true);
            }
        }

        private void OnEnable()
        {
            if (!m_UIManager)
            {
                return;
            }

            m_UIManager.OnViewOpened += HandleViewOpened;
            m_UIManager.OnViewNavigated += HandleViewNavigated;
            m_UIManager.OnViewClosed += HandleViewClosed;
        }

        private void OnDisable()
        {
            if (!m_UIManager)
            {
                return;
            }

            m_UIManager.OnViewOpened -= HandleViewOpened;
            m_UIManager.OnViewNavigated -= HandleViewNavigated;
            m_UIManager.OnViewClosed -= HandleViewClosed;
        }

        private void HandleViewOpened(object key, AbstractView view)
        {
            if (key != null && view)
            {
                m_Views[key] = view;
                ReparentByKey(key, view);
                if (m_LayerPolicy && key is string stringKey && stringKey == UIPrefabKeys.ModalTemplate)
                {
                    m_LayerPolicy.NotifyModalOpened();
                }
            }
        }

        private void HandleViewNavigated(object key, AbstractView view)
        {
            if (key != null && view)
            {
                m_Views[key] = view;
                ReparentByKey(key, view);
            }
        }

        private void HandleViewClosed(object key)
        {
            if (key != null)
            {
                m_Views.Remove(key);
                if (m_LayerPolicy && key is string stringKey && stringKey == UIPrefabKeys.ModalTemplate)
                {
                    m_LayerPolicy.NotifyModalClosed();
                }
            }
        }

        private void ReparentByKey(object key, AbstractView view)
        {
            if (!m_LayerPolicy || !view || key is not string stringKey)
            {
                return;
            }

            Transform targetParent = null;
            switch (stringKey)
            {
                case UIPrefabKeys.HUDRoot:
                    targetParent = m_LayerPolicy.HUDLayer;
                    break;
                case UIPrefabKeys.SidePanel:
                    targetParent = m_LayerPolicy.SidePanelLayer;
                    break;
                case UIPrefabKeys.CombatOverlay:
                    targetParent = m_LayerPolicy.CombatOverlayLayer;
                    break;
                case UIPrefabKeys.ModalTemplate:
                    targetParent = m_LayerPolicy.ModalLayer;
                    break;
            }

            if (targetParent)
            {
                view.Transform.SetParent(targetParent, false);
            }
        }
    }
}
