using UnityEngine;

namespace NeuroSlop
{
    /// <summary>
    /// Side-scrolling camera: follows the player, never scrolls back, locks to an arena during
    /// an encounter and drags two invisible walls along with its bounds.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] float levelMinX;
        [SerializeField] float levelMaxX = 92f;
        [SerializeField] float halfWidth = 7.5f;
        [SerializeField] float fixedY = 2.6f;
        [SerializeField] float smoothTime = 0.18f;
        [SerializeField] float lookAhead = 1.2f;
        [SerializeField] Transform leftWall;
        [SerializeField] Transform rightWall;

        static CameraRig instance;
        static float shakeAmplitude;
        static float shakeUntil;

        float lockMin = float.NegativeInfinity;
        float lockMax = float.PositiveInfinity;
        float progressLeft;
        float velocity;
        float x;

        // Inside an arena the camera may pan both ways; outside, it never scrolls back.
        public float LeftBound => Locked ? Mathf.Max(levelMinX, lockMin) : Mathf.Max(levelMinX, progressLeft);
        public float RightBound => Mathf.Min(levelMaxX, lockMax);
        public bool Locked => !float.IsInfinity(lockMax);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            shakeAmplitude = 0f;
            shakeUntil = 0f;
        }

        void Awake() => instance = this;

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Start()
        {
            x = Mathf.Clamp(target != null ? target.position.x : transform.position.x, levelMinX + halfWidth, levelMaxX - halfWidth);
            progressLeft = x - halfWidth;
            Apply(Vector2.zero);
        }

        public static void Shake(float amplitude, float duration)
        {
            if (Time.unscaledTime > shakeUntil) shakeAmplitude = 0f;
            shakeAmplitude = Mathf.Max(shakeAmplitude, amplitude);
            shakeUntil = Mathf.Max(shakeUntil, Time.unscaledTime + duration);
        }

        public void Lock(float min, float max)
        {
            lockMin = min;
            lockMax = max;
        }

        public void Unlock()
        {
            progressLeft = Mathf.Max(progressLeft, x - halfWidth);
            lockMin = float.NegativeInfinity;
            lockMax = float.PositiveInfinity;
        }

        void LateUpdate()
        {
            float min = LeftBound + halfWidth;
            float max = Mathf.Max(min, RightBound - halfWidth);
            float desired = x;
            if (target != null)
            {
                var player = PlayerController.Instance;
                int facing = player != null ? player.Facing : 1;
                desired = target.position.x + lookAhead * facing;
            }
            desired = Mathf.Clamp(desired, min, max);
            x = Mathf.SmoothDamp(x, desired, ref velocity, smoothTime, Mathf.Infinity, Time.deltaTime);
            if (!Locked) progressLeft = Mathf.Max(progressLeft, x - halfWidth);

            var offset = Vector2.zero;
            if (Time.unscaledTime < shakeUntil) offset = Random.insideUnitCircle * shakeAmplitude;
            float c = CorruptionSystem.Intensity;
            if (Random.value < c * c * 0.03f) offset.x += Random.Range(-0.25f, 0.25f) * c;
            Apply(offset);
        }

        void Apply(Vector2 offset)
        {
            transform.position = new Vector3(x + offset.x, fixedY + offset.y, -10f);
            if (leftWall != null) leftWall.position = new Vector3(LeftBound - 0.5f, leftWall.position.y, 0f);
            if (rightWall != null) rightWall.position = new Vector3(RightBound + 0.5f, rightWall.position.y, 0f);
        }
    }
}
