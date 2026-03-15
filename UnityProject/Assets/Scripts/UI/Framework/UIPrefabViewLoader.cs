using System;
using System.Collections.Generic;
using ToaruUnity.UI;
using UnityEngine;

namespace Minecraft.UI.Framework
{
    [DisallowMultipleComponent]
    public sealed class UIPrefabViewLoader : ViewLoader
    {
        [Serializable]
        private struct Entry
        {
            public string key;
            public AbstractView prefab;
        }

        [SerializeField] private Entry[] m_Entries = Array.Empty<Entry>();
        [SerializeField] private string m_ResourcesPrefix = "UI/";

        private Dictionary<string, AbstractView> m_Cache;

        private void Awake()
        {
            m_Cache = new Dictionary<string, AbstractView>(StringComparer.Ordinal);
            for (int i = 0; i < m_Entries.Length; i++)
            {
                Entry entry = m_Entries[i];
                if (string.IsNullOrWhiteSpace(entry.key) || !entry.prefab)
                {
                    continue;
                }

                m_Cache[entry.key] = entry.prefab;
            }
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

            if (key is string resourceKey)
            {
                AbstractView loaded = Resources.Load<AbstractView>(m_ResourcesPrefix + resourceKey);
                if (loaded)
                {
                    callback(loaded);
                    return;
                }
            }

            Debug.LogWarning($"UIPrefabViewLoader cannot find view prefab for key: {key}");
            callback(null);
        }

        public override void ReleaseViewPrefab(object key, AbstractView prefab)
        {
            // Prefabs are scene/runtime references managed by Unity object lifecycle.
        }
    }
}
