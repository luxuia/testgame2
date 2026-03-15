using UnityEngine;
using UnityEngine.UI;

namespace Minecraft.UI
{
    public class CursorReticle : MonoBehaviour
    {
        [SerializeField] private Image m_ReticleImage;
        [SerializeField] private float m_Sensitivity = 100f;

        private RectTransform m_RectTransform;
        private Canvas m_Canvas;
        private Vector2 m_Offset;

        private void Awake()
        {
            m_RectTransform = GetComponent<RectTransform>();
            m_Canvas = GetComponentInParent<Canvas>();
        }

        private void Update()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                m_ReticleImage.enabled = true;
                UpdateReticlePositionLocked();
            }
            else
            {
                m_ReticleImage.enabled = true;
                UpdateReticlePositionUnlocked();
            }
        }

        private void UpdateReticlePositionLocked()
        {
            m_Offset.x += Input.GetAxis("Mouse X") * m_Sensitivity;
            m_Offset.y += Input.GetAxis("Mouse Y") * m_Sensitivity;

            m_Offset.x = Mathf.Clamp(m_Offset.x, -Screen.width / 2f, Screen.width / 2f);
            m_Offset.y = Mathf.Clamp(m_Offset.y, -Screen.height / 2f, Screen.height / 2f);

            m_RectTransform.anchoredPosition = m_Offset;
        }

        private void UpdateReticlePositionUnlocked()
        {
            Vector2 screenPoint = Input.mousePosition;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                m_RectTransform.parent as RectTransform,
                screenPoint,
                m_Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : m_Canvas.worldCamera,
                out Vector2 localPoint
            );

            m_RectTransform.anchoredPosition = localPoint;
            m_Offset = localPoint;
        }
    }
}