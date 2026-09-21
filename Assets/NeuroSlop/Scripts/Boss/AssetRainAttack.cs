using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NeuroSlop
{
    /// <summary>
    /// "upload(assets)": markers appear on the floor (one always under the player), then
    /// missing-texture blocks drop on them. Counter: step off the marker.
    /// </summary>
    public class AssetRainAttack : BossAttack
    {
        [SerializeField] FallingAsset blockPrefab;
        [SerializeField] SpriteRenderer markerPrefab;
        [SerializeField] int count = 3;
        [SerializeField] float minSpacing = 2.4f;
        [SerializeField] float telegraph = 1.0f;
        [SerializeField] float dropHeight = 9f;
        [SerializeField] float margin = 1.5f;

        readonly List<GameObject> spawned = new();

        public override IEnumerator Run(BossController boss, float speed)
        {
            spawned.RemoveAll(go => go == null);
            Announce(consoleLine);
            boss.SetFace(BossController.Face.Charge);

            var xs = PickPositions(boss, Mathf.RoundToInt(count * Mathf.Max(1f, speed)));
            var markers = new List<SpriteRenderer>();
            foreach (float x in xs)
            {
                var m = Instantiate(markerPrefab, new Vector3(x, 0.02f, -0.2f), Quaternion.identity);
                markers.Add(m);
                spawned.Add(m.gameObject);
            }

            float t = telegraph / speed;
            while (t > 0f)
            {
                t -= Time.deltaTime;
                foreach (var m in markers)
                    if (m != null) m.enabled = Mathf.Repeat(t * 10f, 1f) < 0.6f;
                yield return null;
            }

            var blocks = new List<FallingAsset>();
            for (int i = 0; i < xs.Count; i++)
            {
                var b = Instantiate(blockPrefab, new Vector3(xs[i], dropHeight, -0.3f), Quaternion.identity);
                blocks.Add(b);
                spawned.Add(b.gameObject);
                yield return new WaitForSeconds(0.08f);
            }

            while (blocks.Exists(b => b != null && !b.Landed)) yield return null;
            foreach (var m in markers)
                if (m != null) Destroy(m.gameObject);
            boss.SetFace(BossController.Face.Idle);
        }

        List<float> PickPositions(BossController boss, int n)
        {
            float min = boss.ArenaMin + margin;
            float max = boss.ArenaMax - margin;
            var result = new List<float>();
            var player = PlayerController.Instance;
            if (player != null) result.Add(Mathf.Clamp(player.transform.position.x, min, max));
            int guard = 0;
            while (result.Count < n && guard++ < 60)
            {
                float x = Random.Range(min, max);
                if (result.TrueForAll(o => Mathf.Abs(o - x) >= minSpacing)) result.Add(x);
            }
            return result;
        }

        public override void Cancel()
        {
            foreach (var go in spawned)
                if (go != null) Destroy(go);
            spawned.Clear();
        }
    }
}
