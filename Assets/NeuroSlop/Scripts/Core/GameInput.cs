using UnityEngine.InputSystem;

namespace NeuroSlop
{
    /// <summary>
    /// Thin access to the project-wide Input Actions (Assets/Settings/InputSystem_Actions).
    /// Keyboard and gamepad come from the same actions.
    /// </summary>
    public static class GameInput
    {
        public static InputAction Find(string path)
        {
            var actions = InputSystem.actions;
            if (actions == null) return null;
            if (!actions.enabled) actions.Enable();
            return actions.FindAction(path);
        }

        public static bool Pressed(string path)
        {
            var action = Find(path);
            return action != null && action.WasPressedThisFrame();
        }

        public static bool ConfirmPressed() => Pressed("UI/Submit") || Pressed("Player/Attack") || Pressed("Player/Jump");
        public static bool PausePressed() => Pressed("Player/Pause");
        public static bool BackPressed() => Pressed("Player/Back");
    }
}
