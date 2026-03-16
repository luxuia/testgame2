using System.Collections.Generic;
using UnityEngine;

namespace Minecraft.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatFeedbackView : MonoBehaviour
    {
        [Header("Health Bar Overlay")]
        [SerializeField] private bool m_ShowHealthBar = true;
        [SerializeField] private Vector3 m_HealthBarOffset = new Vector3(0f, 2.1f, 0f);
        [SerializeField] private string m_DisplayNameOverride;
        [SerializeField] [TextArea] private string m_BuffSummaryOverride;

        [Header("Floating Damage Text")]
        [SerializeField] private bool m_ShowFloatingDamage = true;
        [SerializeField] private Vector3 m_DamageTextOffset = new Vector3(0f, 2.35f, 0f);
        [SerializeField] [Min(0.1f)] private float m_DamageTextLifetime = 0.75f;
        [SerializeField] [Min(0.05f)] private float m_DamageTextRise = 0.65f;
        [SerializeField] [Min(0f)] private float m_DamageTextHorizontalJitter = 0.22f;
        [SerializeField] [Min(0.01f)] private float m_DamageTextScale = 0.08f;
        [SerializeField] private Color m_DamageTextColor = new Color(1f, 0.26f, 0.26f, 1f);

        private struct FloatingTextRuntime
        {
            public Transform Transform;
            public TextMesh TextMesh;
            public Vector3 StartWorldPosition;
            public float SpawnedAt;
        }

        private readonly List<FloatingTextRuntime> m_FloatingTexts = new List<FloatingTextRuntime>(8);

        private CombatOverlayUIManager m_OverlayManager;
        private Font m_Font;
        private float m_CurrentHealth = 1f;
        private float m_MaxHealth = 1f;
        private bool m_IsInitialized;
        private string m_RuntimeDisplayName;
        private string m_RuntimeBuffSummary;

        public void EnsureInitialized()
        {
            if (m_IsInitialized)
            {
                return;
            }

            m_IsInitialized = true;
            PushOverlayState();
        }

        public void SetHealth(float currentHealth, float maxHealth)
        {
            m_MaxHealth = Mathf.Max(1f, maxHealth);
            m_CurrentHealth = Mathf.Clamp(currentHealth, 0f, m_MaxHealth);
            PushOverlayState();
        }

        public void SetOverlayMeta(string displayName, string buffSummary = null)
        {
            m_RuntimeDisplayName = displayName;
            m_RuntimeBuffSummary = buffSummary;
            PushOverlayState();
        }

        public void SetHealthBarVisible(bool visible)
        {
            if (m_ShowHealthBar == visible)
            {
                return;
            }

            m_ShowHealthBar = visible;
            if (!m_ShowHealthBar)
            {
                UnregisterOverlay();
                return;
            }

            PushOverlayState();
        }

        public void ShowDamage(float damage)
        {
            if (!m_ShowFloatingDamage || damage <= 0f)
            {
                return;
            }

            Font runtimeFont = ResolveRuntimeFont();
            if (runtimeFont == null)
            {
                return;
            }

            GameObject go = new GameObject("CombatDamageText");
            go.transform.SetParent(transform, false);

            float jitterX = Random.Range(-m_DamageTextHorizontalJitter, m_DamageTextHorizontalJitter);
            go.transform.position = transform.position + m_DamageTextOffset + new Vector3(jitterX, 0f, 0f);

            TextMesh textMesh = go.AddComponent<TextMesh>();
            textMesh.font = runtimeFont;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = Mathf.Max(0.01f, m_DamageTextScale);
            textMesh.fontSize = 48;
            textMesh.text = $"-{Mathf.Max(1, Mathf.RoundToInt(damage))}";
            textMesh.color = m_DamageTextColor;

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && runtimeFont.material != null)
            {
                renderer.sharedMaterial = runtimeFont.material;
            }

            m_FloatingTexts.Add(new FloatingTextRuntime
            {
                Transform = go.transform,
                TextMesh = textMesh,
                StartWorldPosition = go.transform.position,
                SpawnedAt = Time.time,
            });
        }

        private void OnEnable()
        {
            if (!m_IsInitialized)
            {
                return;
            }

            PushOverlayState();
        }

        private void OnDisable()
        {
            UnregisterOverlay();
        }

        private void LateUpdate()
        {
            UpdateFloatingTexts();
        }

        private void OnDestroy()
        {
            UnregisterOverlay();
        }

        private void PushOverlayState()
        {
            if (!m_ShowHealthBar || !isActiveAndEnabled)
            {
                return;
            }

            EnsureOverlayManager();
            if (m_OverlayManager == null)
            {
                return;
            }

            m_OverlayManager.RegisterOrUpdate(
                this,
                transform,
                m_HealthBarOffset,
                m_CurrentHealth,
                m_MaxHealth,
                ResolveDisplayName(),
                ResolveBuffSummary());
        }

        private void EnsureOverlayManager()
        {
            if (m_OverlayManager == null)
            {
                m_OverlayManager = CombatOverlayUIManager.EnsureInstance();
            }
        }

        private void UnregisterOverlay()
        {
            if (m_OverlayManager == null)
            {
                return;
            }

            m_OverlayManager.Unregister(this);
        }

        private string ResolveDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(m_RuntimeDisplayName))
            {
                return m_RuntimeDisplayName;
            }

            if (!string.IsNullOrWhiteSpace(m_DisplayNameOverride))
            {
                return m_DisplayNameOverride;
            }

            return gameObject.name;
        }

        private string ResolveBuffSummary()
        {
            if (!string.IsNullOrWhiteSpace(m_RuntimeBuffSummary))
            {
                return m_RuntimeBuffSummary;
            }

            return m_BuffSummaryOverride;
        }

        private void UpdateFloatingTexts()
        {
            if (m_FloatingTexts.Count == 0)
            {
                return;
            }

            Camera cam = Camera.main;
            for (int i = m_FloatingTexts.Count - 1; i >= 0; i--)
            {
                FloatingTextRuntime runtime = m_FloatingTexts[i];
                if (runtime.Transform == null || runtime.TextMesh == null)
                {
                    m_FloatingTexts.RemoveAt(i);
                    continue;
                }

                float elapsed = Time.time - runtime.SpawnedAt;
                float t = m_DamageTextLifetime > 0.001f
                    ? Mathf.Clamp01(elapsed / m_DamageTextLifetime)
                    : 1f;
                if (t >= 1f)
                {
                    Destroy(runtime.Transform.gameObject);
                    m_FloatingTexts.RemoveAt(i);
                    continue;
                }

                runtime.Transform.position = runtime.StartWorldPosition + Vector3.up * (m_DamageTextRise * t);
                Color c = m_DamageTextColor;
                c.a *= 1f - t;
                runtime.TextMesh.color = c;

                if (cam != null)
                {
                    Vector3 toText = runtime.Transform.position - cam.transform.position;
                    if (toText.sqrMagnitude > 0.0001f)
                    {
                        runtime.Transform.rotation = Quaternion.LookRotation(toText.normalized, Vector3.up);
                    }
                }

                m_FloatingTexts[i] = runtime;
            }
        }

        private Font ResolveRuntimeFont()
        {
            if (m_Font != null)
            {
                return m_Font;
            }

            // Unity 2022+ removed Arial as built-in runtime font.
            m_Font = TryGetBuiltinFont("LegacyRuntime.ttf");
            if (m_Font == null)
            {
                m_Font = TryGetBuiltinFont("Arial.ttf");
            }

            if (m_Font == null)
            {
                m_Font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Arial", "Segoe UI", "Microsoft YaHei UI", "Microsoft YaHei" },
                    16);
            }

            return m_Font;
        }

        private static Font TryGetBuiltinFont(string fontPath)
        {
            if (string.IsNullOrWhiteSpace(fontPath))
            {
                return null;
            }

            try
            {
                return Resources.GetBuiltinResource<Font>(fontPath);
            }
            catch (System.ArgumentException)
            {
                return null;
            }
        }
    }
}
