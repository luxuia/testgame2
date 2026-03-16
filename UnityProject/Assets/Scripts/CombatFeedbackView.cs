using System.Collections.Generic;
using UnityEngine;

namespace Minecraft.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatFeedbackView : MonoBehaviour
    {
        [Header("Health Bar")]
        [SerializeField] private bool m_ShowHealthBar = true;
        [SerializeField] private Vector3 m_HealthBarOffset = new Vector3(0f, 2.1f, 0f);
        [SerializeField] [Min(0.2f)] private float m_HealthBarWidth = 1.35f;
        [SerializeField] [Min(0.01f)] private float m_HealthBarThickness = 0.06f;
        [SerializeField] private Color m_HealthBarBackColor = new Color(0f, 0f, 0f, 0.72f);
        [SerializeField] private Color m_HealthBarFillColor = new Color(0.18f, 0.92f, 0.35f, 0.95f);

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

        private Transform m_BarRoot;
        private LineRenderer m_BackLine;
        private LineRenderer m_FillLine;
        private Material m_LineMaterial;
        private Font m_Font;

        private float m_CurrentHealth = 1f;
        private float m_MaxHealth = 1f;
        private bool m_IsInitialized;

        public void EnsureInitialized()
        {
            if (m_IsInitialized)
            {
                return;
            }

            m_IsInitialized = true;
            if (!m_ShowHealthBar)
            {
                return;
            }

            m_LineMaterial = BuildLineMaterial();
            GameObject root = new GameObject("CombatHealthBarRoot");
            root.transform.SetParent(transform, false);
            m_BarRoot = root.transform;

            m_BackLine = CreateLineRenderer("Back", m_HealthBarBackColor, m_HealthBarThickness);
            m_BackLine.transform.SetParent(m_BarRoot, false);
            m_BackLine.SetPosition(0, new Vector3(-m_HealthBarWidth * 0.5f, 0f, 0f));
            m_BackLine.SetPosition(1, new Vector3(m_HealthBarWidth * 0.5f, 0f, 0f));

            m_FillLine = CreateLineRenderer("Fill", m_HealthBarFillColor, m_HealthBarThickness * 0.8f);
            m_FillLine.transform.SetParent(m_BarRoot, false);
            UpdateHealthBar();
        }

        public void SetHealth(float currentHealth, float maxHealth)
        {
            m_MaxHealth = Mathf.Max(1f, maxHealth);
            m_CurrentHealth = Mathf.Clamp(currentHealth, 0f, m_MaxHealth);
            if (m_ShowHealthBar)
            {
                EnsureInitialized();
                UpdateHealthBar();
            }
        }

        public void ShowDamage(float damage)
        {
            if (!m_ShowFloatingDamage || damage <= 0f)
            {
                return;
            }

            if (m_Font == null)
            {
                m_Font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            GameObject go = new GameObject("CombatDamageText");
            go.transform.SetParent(transform, false);

            float jitterX = Random.Range(-m_DamageTextHorizontalJitter, m_DamageTextHorizontalJitter);
            go.transform.position = transform.position + m_DamageTextOffset + new Vector3(jitterX, 0f, 0f);

            TextMesh textMesh = go.AddComponent<TextMesh>();
            textMesh.font = m_Font;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = Mathf.Max(0.01f, m_DamageTextScale);
            textMesh.fontSize = 48;
            textMesh.text = $"-{Mathf.Max(1, Mathf.RoundToInt(damage))}";
            textMesh.color = m_DamageTextColor;

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && m_Font != null && m_Font.material != null)
            {
                renderer.sharedMaterial = m_Font.material;
            }

            m_FloatingTexts.Add(new FloatingTextRuntime
            {
                Transform = go.transform,
                TextMesh = textMesh,
                StartWorldPosition = go.transform.position,
                SpawnedAt = Time.time,
            });
        }

        private void LateUpdate()
        {
            UpdateHealthBarTransform();
            UpdateFloatingTexts();
        }

        private void OnDestroy()
        {
            if (m_LineMaterial != null)
            {
                Destroy(m_LineMaterial);
                m_LineMaterial = null;
            }
        }

        private void UpdateHealthBarTransform()
        {
            if (!m_ShowHealthBar || m_BarRoot == null)
            {
                return;
            }

            m_BarRoot.position = transform.position + m_HealthBarOffset;
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector3 toBar = m_BarRoot.position - cam.transform.position;
            if (toBar.sqrMagnitude > 0.0001f)
            {
                m_BarRoot.rotation = Quaternion.LookRotation(toBar.normalized, Vector3.up);
            }
        }

        private void UpdateHealthBar()
        {
            if (m_FillLine == null)
            {
                return;
            }

            float ratio = Mathf.Clamp01(m_CurrentHealth / Mathf.Max(1f, m_MaxHealth));
            float left = -m_HealthBarWidth * 0.5f;
            float right = left + m_HealthBarWidth * ratio;
            m_FillLine.SetPosition(0, new Vector3(left, 0f, -0.01f));
            m_FillLine.SetPosition(1, new Vector3(right, 0f, -0.01f));
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

        private LineRenderer CreateLineRenderer(string name, Color color, float width)
        {
            GameObject go = new GameObject(name);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.numCapVertices = 2;
            line.alignment = LineAlignment.View;
            line.widthMultiplier = Mathf.Max(0.005f, width);
            line.material = m_LineMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.textureMode = LineTextureMode.Stretch;
            line.startColor = color;
            line.endColor = color;
            return line;
        }

        private static Material BuildLineMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            return new Material(shader);
        }
    }
}
