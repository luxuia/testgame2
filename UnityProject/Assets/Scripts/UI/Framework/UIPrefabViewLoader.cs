using System;
using System.Collections.Generic;
using ToaruUnity.UI;
using UnityEngine;

namespace Minecraft.UI.Framework
{
    [DisallowMultipleComponent]
    public sealed class UIPrefabViewLoader : ViewLoader
    {
        [Header("Core Prefabs")]
        [SerializeField] private GameObject m_HUDRootPrefab;
        [SerializeField] private GameObject m_SidePanelPrefab;
        [SerializeField] private GameObject m_ModalTemplatePrefab;
        [SerializeField] private GameObject m_CombatOverlayPrefab;

        private Dictionary<string, AbstractView> m_Cache;

        private void Awake()
        {
            m_Cache = new Dictionary<string, AbstractView>(StringComparer.Ordinal);

            RegisterCore(UIPrefabKeys.HUDRoot, m_HUDRootPrefab);
            RegisterCore(UIPrefabKeys.SidePanel, m_SidePanelPrefab);
            RegisterCore(UIPrefabKeys.ModalTemplate, m_ModalTemplatePrefab);
            RegisterCore(UIPrefabKeys.CombatOverlay, m_CombatOverlayPrefab);
        }

        public override void LoadViewPrefab(object key, Action<AbstractView> callback)
        {
            if (callback == null)
            {
                return;
            }

            if (key is string stringKey && m_Cache != null && m_Cache.TryGetValue(stringKey, out AbstractView prefab))
            {
                callback(prefab);
                return;
            }

            Debug.LogWarning($"UIPrefabViewLoader cannot find view prefab for key: {key}");
            callback(null);
        }

        public override void ReleaseViewPrefab(object key, AbstractView prefab)
        {
            // Prefabs are scene/runtime references managed by Unity object lifecycle.
        }

        private void RegisterCore(string key, GameObject prefabObject)
        {
            if (string.IsNullOrEmpty(key) || !prefabObject)
            {
                return;
            }

            if (!prefabObject.TryGetComponent(out AbstractView view))
            {
                Debug.LogWarning($"Core prefab {prefabObject.name} is missing AbstractView component.");
                return;
            }

            m_Cache[key] = view;
        }
    }
}
