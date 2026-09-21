using UnityEngine;
using UnityEngine.InputSystem;

namespace NeuroSlop
{
    /// <summary>
    /// Reads Player/Move, Player/Jump, Player/Attack from the project-wide Input Actions
    /// (keyboard + gamepad) and buffers presses so the controller can consume them.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] float deadZone = 0.25f;

        InputAction move;
        InputAction jump;
        InputAction attack;
        float jumpPressedAt = float.NegativeInfinity;
        float attackPressedAt = float.NegativeInfinity;

        public float MoveX { get; private set; }
        public bool JumpHeld => isActiveAndEnabled && jump != null && jump.IsPressed();

        void OnEnable()
        {
            move = GameInput.Find("Player/Move");
            jump = GameInput.Find("Player/Jump");
            attack = GameInput.Find("Player/Attack");
            if (move == null || jump == null || attack == null)
                Debug.LogError("NeuroSlop: Player/Move, Jump or Attack not found in project-wide Input Actions.");
            ClearBuffers();
        }

        void OnDisable()
        {
            MoveX = 0f;
            ClearBuffers();
        }

        void Update()
        {
            if (TimeControl.Paused)
            {
                MoveX = 0f;
                return;
            }
            float x = move != null ? move.ReadValue<Vector2>().x : 0f;
            MoveX = Mathf.Abs(x) < deadZone ? 0f : Mathf.Sign(x);
            if (jump != null && jump.WasPressedThisFrame()) jumpPressedAt = Time.time;
            if (attack != null && attack.WasPressedThisFrame()) attackPressedAt = Time.time;
        }

        public bool ConsumeJump(float buffer) => Consume(ref jumpPressedAt, buffer);
        public bool ConsumeAttack(float buffer) => Consume(ref attackPressedAt, buffer);

        public void ClearBuffers()
        {
            jumpPressedAt = float.NegativeInfinity;
            attackPressedAt = float.NegativeInfinity;
        }

        static bool Consume(ref float pressedAt, float buffer)
        {
            if (Time.time - pressedAt > buffer) return false;
            pressedAt = float.NegativeInfinity;
            return true;
        }
    }
}
