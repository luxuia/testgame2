using UnityEngine;

namespace Minecraft.PlayerControls
{
    /// <summary>
    /// 规范化后的玩家输入命令类型。
    /// </summary>
    public enum PlayerCommandType
    {
        MovePrimaryDown = 0,
        MineSecondaryDown = 1,
        MineSecondaryHold = 2,
        MineSecondaryUp = 3,
        CancelSelection = 4,
        ToggleSidePanel = 5,
        ToggleModal = 6,
        ToggleCombatOverlay = 7,
        ForwardTap = 8,
    }

    /// <summary>
    /// 单帧输入命令载体。
    /// 对于鼠标类命令，附带屏幕坐标；键盘命令不附带坐标。
    /// </summary>
    public readonly struct PlayerCommand
    {
        public PlayerCommandType Type { get; }
        public Vector2 ScreenPosition { get; }
        public bool HasScreenPosition { get; }

        private PlayerCommand(PlayerCommandType type, Vector2 screenPosition, bool hasScreenPosition)
        {
            Type = type;
            ScreenPosition = screenPosition;
            HasScreenPosition = hasScreenPosition;
        }

        public static PlayerCommand Create(PlayerCommandType type)
        {
            return new PlayerCommand(type, Vector2.zero, false);
        }

        public static PlayerCommand CreatePointer(PlayerCommandType type, Vector2 screenPosition)
        {
            return new PlayerCommand(type, screenPosition, true);
        }
    }
}
