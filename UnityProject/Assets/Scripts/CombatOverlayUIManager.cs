using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Minecraft.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatOverlayUIManager : MonoBehaviour
    {
        private const float HideMarginPixels = 60f;

        private sealed class OverlayEntry
        {
            public int Key;
            public CombatFeedbackView Owner;
            public Transform Target;
            public Vector3 WorldOffset;
            public float CurrentHealth;
            public float MaxHealth;
            public string DisplayName;
            public string BuffSummary;
            public RectTransform Root;
            public Image FillImage;
            public Text NameText;
            public Text BuffText;
        }

        private static CombatOverlayUIManager s_Instance;

        [Header("Bar Style")]
        [SerializeField] private Vector2 m_BarSize = new Vector2(90f, 9f);
        [SerializeField] private Color m_BarBackColor = new Color(0f, 0f, 0f, 0.68f);
        [SerializeField] private Color m_BarFillColor = new Color(0.17f, 0.9f, 0.34f, 0.96f);

        [Header("Text Style")]
        [SerializeField] private Color m_NameColor = new Color(1f, 1f, 1f, 0.96f);
        [SerializeField] private Color m_BuffColor = new Color(0.89f, 0.95f, 1f, 0.94f);
        [SerializeField] private int m_NameFontSize = 13;
        [SerializeField] private int m_BuffFontSize = 12;
        [SerializeField] private Vector2 m_NameOffset = new Vector2(0f, 12f);
        [SerializeField] private Vector2 m_BuffOffset = new Vector2(0f, -12f);

        private readonly Dictionary<int, OverlayEntry> m_Entries = new Dictionary<int, OverlayEntry>(128);
        private readonly List<int> m_RemoveBuffer = new List<int>(32);

        private Canvas m_Canvas;
        private RectTransform m_CanvasRect;
        private Font m_Font;

        public static CombatOverlayUIManager EnsureInstance()
        {
            if (s_Instance != null)
            {
                return s_Instance;
            }

            s_Instance = FindObjectOfType<CombatOverlayUIManager>();
            if (s_Instance != null)
            {
                return s_Instance;
            }

            GameObject go = new GameObject("CombatOverlayUIManager");
            s_Instance = go.AddComponent<CombatOverlayUIManager>();
            return s_Instance;
        }

        public void RegisterOrUpdate(
            CombatFeedbackView owner,
            Transform target,
            Vector3 worldOffset,
            float currentHealth,
            float maxHealth,
            string displayName,
            string buffSummary)
        {
            if (owner == null || target == null)
            {
                return;
            }

            EnsureCanvasIfNeeded();

            int key = owner.GetInstanceID();
            if (!m_Entries.TryGetValue(key, out OverlayEntry entry))
            {
                entry = CreateEntry(key);
                m_Entries.Add(key, entry);
            }

            entry.Owner = owner;
            entry.Target = target;
            entry.WorldOffset = worldOffset;
            entry.CurrentHealth = Mathf.Max(0f, currentHealth);
            entry.MaxHealth = Mathf.Max(1f, maxHealth);
            entry.DisplayName = displayName;
            entry.BuffSummary = buffSummary;

            ApplyEntryVisuals(entry);
        }

        public void Unregister(CombatFeedbackView owner)
        {
            if (owner == null)
            {
                return;
            }

            int key = owner.GetInstanceID();
            if (!m_Entries.TryGetValue(key, out OverlayEntry entry))
            {
                return;
            }

            DestroyEntry(entry);
            m_Entries.Remove(key);
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureCanvasIfNeeded();
        }

        private void LateUpdate()
        {
            if (m_Entries.Count == 0)
            {
                return;
            }

            Camera worldCamera = ResolveWorldCamera();
            Camera uiCamera = m_Canvas != null && m_Canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : m_Canvas != null
                    ? m_Canvas.worldCamera
                    : null;

            m_RemoveBuffer.Clear();
            foreach (KeyValuePair<int, OverlayEntry> pair in m_Entries)
            {
                OverlayEntry entry = pair.Value;
                if (entry == null || entry.Owner == null || entry.Target == null || entry.Root == null)
                {
                    m_RemoveBuffer.Add(pair.Key);
                    continue;
                }

                if (!entry.Target.gameObject.activeInHierarchy || worldCamera == null)
                {
                    SetEntryVisible(entry, false);
                    continue;
                }

                Vector3 worldPos = entry.Target.position + entry.WorldOffset;
                Vector3 screenPos = worldCamera.WorldToScreenPoint(worldPos);
                bool visible = screenPos.z > 0f
                               && screenPos.x >= -HideMarginPixels
                               && screenPos.x <= Screen.width + HideMarginPixels
                               && screenPos.y >= -HideMarginPixels
                               && screenPos.y <= Screen.height + HideMarginPixels;
                if (!visible)
                {
                    SetEntryVisible(entry, false);
                    continue;
                }

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        m_CanvasRect,
                        screenPos,
                        uiCamera,
                        out Vector2 localPos))
                {
                    SetEntryVisible(entry, true);
                    entry.Root.anchoredPosition = localPos;
                }
                else
                {
                    SetEntryVisible(entry, false);
                }
            }

            for (int i = 0; i < m_RemoveBuffer.Count; i++)
            {
                int key = m_RemoveBuffer[i];
                if (!m_Entries.TryGetValue(key, out OverlayEntry entry))
                {
                    continue;
                }

                DestroyEntry(entry);
                m_Entries.Remove(key);
            }
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }

            foreach (OverlayEntry entry in m_Entries.Values)
            {
                DestroyEntry(entry);
            }

            m_Entries.Clear();
        }

        private void EnsureCanvasIfNeeded()
        {
            if (m_Canvas != null && m_CanvasRect != null)
            {
                return;
            }

            GameObject canvasGo = new GameObject(
                "CombatOverlayCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);

            m_Canvas = canvasGo.GetComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            m_Canvas.sortingOrder = 450;
            m_Canvas.pixelPerfect = false;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster raycaster = canvasGo.GetComponent<GraphicRaycaster>();
            raycaster.enabled = false;

            CanvasGroup group = canvasGo.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 1f;

            m_CanvasRect = canvasGo.GetComponent<RectTransform>();
            m_CanvasRect.anchorMin = Vector2.zero;
            m_CanvasRect.anchorMax = Vector2.one;
            m_CanvasRect.offsetMin = Vector2.zero;
            m_CanvasRect.offsetMax = Vector2.zero;
        }

        private static Camera ResolveWorldCamera()
        {
            Camera taggedMain = Camera.main;
            if (taggedMain != null && taggedMain.isActiveAndEnabled)
            {
                return taggedMain;
            }

            if (World.Active is World world && world.MainCamera != null && world.MainCamera.isActiveAndEnabled)
            {
                return world.MainCamera;
            }

            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].isActiveAndEnabled)
                {
                    return cameras[i];
                }
            }

            return null;
        }

        private OverlayEntry CreateEntry(int key)
        {
            OverlayEntry entry = new OverlayEntry { Key = key };

            GameObject rootGo = new GameObject($"CombatOverlay_{key}", typeof(RectTransform));
            rootGo.transform.SetParent(m_CanvasRect, false);
            entry.Root = rootGo.GetComponent<RectTransform>();
            entry.Root.anchorMin = new Vector2(0.5f, 0.5f);
            entry.Root.anchorMax = new Vector2(0.5f, 0.5f);
            entry.Root.pivot = new Vector2(0.5f, 0.5f);
            entry.Root.sizeDelta = new Vector2(Mathf.Max(100f, m_BarSize.x + 10f), 40f);

            Image backImage = CreateImage("HealthBarBack", entry.Root, m_BarBackColor);
            RectTransform backRt = backImage.rectTransform;
            backRt.anchorMin = new Vector2(0.5f, 0.5f);
            backRt.anchorMax = new Vector2(0.5f, 0.5f);
            backRt.pivot = new Vector2(0.5f, 0.5f);
            backRt.anchoredPosition = Vector2.zero;
            backRt.sizeDelta = m_BarSize;

            Image fillImage = CreateImage("HealthBarFill", backRt, m_BarFillColor);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;
            RectTransform fillRt = fillImage.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            entry.FillImage = fillImage;

            Text nameText = CreateText("NameText", entry.Root, m_NameColor, Mathf.Max(10, m_NameFontSize));
            RectTransform nameRt = nameText.rectTransform;
            nameRt.anchorMin = new Vector2(0.5f, 0.5f);
            nameRt.anchorMax = new Vector2(0.5f, 0.5f);
            nameRt.pivot = new Vector2(0.5f, 0.5f);
            nameRt.anchoredPosition = m_NameOffset;
            nameRt.sizeDelta = new Vector2(220f, 20f);
            nameText.alignment = TextAnchor.LowerCenter;
            entry.NameText = nameText;

            Text buffText = CreateText("BuffText", entry.Root, m_BuffColor, Mathf.Max(10, m_BuffFontSize));
            RectTransform buffRt = buffText.rectTransform;
            buffRt.anchorMin = new Vector2(0.5f, 0.5f);
            buffRt.anchorMax = new Vector2(0.5f, 0.5f);
            buffRt.pivot = new Vector2(0.5f, 0.5f);
            buffRt.anchoredPosition = m_BuffOffset;
            buffRt.sizeDelta = new Vector2(260f, 18f);
            buffText.alignment = TextAnchor.UpperCenter;
            entry.BuffText = buffText;

            SetEntryVisible(entry, true);
            return entry;
        }

        private void ApplyEntryVisuals(OverlayEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            float ratio = Mathf.Clamp01(entry.CurrentHealth / Mathf.Max(1f, entry.MaxHealth));
            if (entry.FillImage != null)
            {
                entry.FillImage.fillAmount = ratio;
            }

            if (entry.NameText != null)
            {
                bool hasName = !string.IsNullOrWhiteSpace(entry.DisplayName);
                entry.NameText.gameObject.SetActive(hasName);
                if (hasName)
                {
                    entry.NameText.text = entry.DisplayName;
                }
            }

            if (entry.BuffText != null)
            {
                bool hasBuff = !string.IsNullOrWhiteSpace(entry.BuffSummary);
                entry.BuffText.gameObject.SetActive(hasBuff);
                if (hasBuff)
                {
                    entry.BuffText.text = entry.BuffSummary;
                }
            }
        }

        private static void SetEntryVisible(OverlayEntry entry, bool visible)
        {
            if (entry?.Root == null)
            {
                return;
            }

            if (entry.Root.gameObject.activeSelf != visible)
            {
                entry.Root.gameObject.SetActive(visible);
            }
        }

        private static void DestroyEntry(OverlayEntry entry)
        {
            if (entry?.Root != null)
            {
                Destroy(entry.Root.gameObject);
            }
        }

        private Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateText(string name, Transform parent, Color color, int fontSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            Text text = go.GetComponent<Text>();
            text.font = ResolveRuntimeFont();
            text.fontSize = fontSize;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private Font ResolveRuntimeFont()
        {
            if (m_Font != null)
            {
                return m_Font;
            }

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
