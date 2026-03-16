using ToaruUnity.UI;
using UnityEngine;

namespace Minecraft.UI.Framework
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class CombatOverlayView : AbstractUGUIView
    {
        [SerializeField] private GameObject m_BattleInfoRoot;
        [SerializeField] private GameObject m_BossWarningAnchor;

        protected override void OnCreate()
        {
            base.OnCreate();
            if (!m_BattleInfoRoot)
            {
                Transform battleInfo = Transform.Find("BattleInfoRoot");
                if (battleInfo) m_BattleInfoRoot = battleInfo.gameObject;
            }

            if (!m_BossWarningAnchor)
            {
                Transform bossWarning = Transform.Find("BossWarningAnchor");
                if (bossWarning) m_BossWarningAnchor = bossWarning.gameObject;
            }
        }

        public void SetCombatVisible(bool visible)
        {
            if (m_BattleInfoRoot)
            {
                m_BattleInfoRoot.SetActive(visible);
            }

            if (!visible && m_BossWarningAnchor)
            {
                m_BossWarningAnchor.SetActive(false);
            }
        }
    }
}
