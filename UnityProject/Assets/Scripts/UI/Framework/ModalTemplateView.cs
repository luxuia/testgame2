using ToaruUnity.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Minecraft.UI.Framework
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ModalTemplateView : AbstractUGUIView
    {
        [Header("Slots")]
        [SerializeField] private RectTransform m_TitleSlot;
        [SerializeField] private RectTransform m_ContentRoot;
        [SerializeField] private RectTransform m_PaginationSlot;
        [SerializeField] private Button m_CloseButton;

        public RectTransform TitleSlot => m_TitleSlot;
        public RectTransform ContentRoot => m_ContentRoot;
        public RectTransform PaginationSlot => m_PaginationSlot;

        protected override void OnCreate()
        {
            base.OnCreate();
            ResolveSlotsIfNeeded();
            if (m_CloseButton)
            {
                m_CloseButton.onClick.AddListener(CloseSelf);
            }
        }

        protected override void OnDestroy()
        {
            if (m_CloseButton)
            {
                m_CloseButton.onClick.RemoveListener(CloseSelf);
            }

            base.OnDestroy();
        }

        private void CloseSelf()
        {
            gameObject.SetActive(false);
        }

        private void ResolveSlotsIfNeeded()
        {
            if (!m_TitleSlot) m_TitleSlot = Transform.Find("TitleSlot") as RectTransform;
            if (!m_ContentRoot) m_ContentRoot = Transform.Find("ContentRoot") as RectTransform;
            if (!m_PaginationSlot) m_PaginationSlot = Transform.Find("PaginationSlot") as RectTransform;

            if (!m_CloseButton)
            {
                Transform close = Transform.Find("CloseButton");
                if (close)
                {
                    m_CloseButton = close.GetComponent<Button>();
                }
            }
        }
    }
}
