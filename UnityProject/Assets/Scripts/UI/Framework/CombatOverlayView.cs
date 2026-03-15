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
