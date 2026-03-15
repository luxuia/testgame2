using ToaruUnity.UI;
using UnityEngine;

namespace Minecraft.UI.Framework
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class HUDRootView : AbstractUGUIView
    {
        [Header("Slots")]
        [SerializeField] private RectTransform m_StatusSlot;
        [SerializeField] private RectTransform m_HotbarSlot;
        [SerializeField] private RectTransform m_ManaCapacitySlot;
        [SerializeField] private RectTransform m_FixedButtonsSlot;

        public RectTransform StatusSlot => m_StatusSlot;
        public RectTransform HotbarSlot => m_HotbarSlot;
        public RectTransform ManaCapacitySlot => m_ManaCapacitySlot;
        public RectTransform FixedButtonsSlot => m_FixedButtonsSlot;

        protected override void OnCreate()
        {
            base.OnCreate();
            ResolveSlotsIfNeeded();
        }

        private void ResolveSlotsIfNeeded()
        {
            if (!m_StatusSlot) m_StatusSlot = Transform.Find("StatusSlot") as RectTransform;
            if (!m_HotbarSlot) m_HotbarSlot = Transform.Find("HotbarSlot") as RectTransform;
            if (!m_ManaCapacitySlot) m_ManaCapacitySlot = Transform.Find("ManaCapacitySlot") as RectTransform;
            if (!m_FixedButtonsSlot) m_FixedButtonsSlot = Transform.Find("FixedButtonsSlot") as RectTransform;
        }
    }
}
