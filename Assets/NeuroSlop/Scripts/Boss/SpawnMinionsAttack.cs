using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NeuroSlop
{
    /// <summary>
    /// "generate(enemy)": a portal with a progress bar fills up, then regular enemies appear.
    /// Counter: punch the boss while they load, or clear them quickly.
    /// </summary>
    public class SpawnMinionsAttack : BossAttack
    {
        [SerializeField] EnemyController[] prefabs;
        [SerializeField] SpriteRenderer portalPrefab;
        [SerializeField] int perCast = 2;
        [SerializeField] int maxAlive = 2;
        [SerializeField] float generateTime = 1.4f;

        readonly List<EnemyController> alive = new();
        readonly List<GameObject> portals = new();

        public override bool CanRun(BossController boss)
        {
            alive.RemoveAll(e => e == null || e.IsDead);
            return alive.Count < maxAlive && prefabs.Length > 0;
        }

        public override IEnumerator Run(BossController boss, float speed)
        {
            Announce(consoleLine);
            boss.SetFace(BossController.Face.Charge);

            int n = Mathf.Min(perCast, maxAlive - alive.Count);
            var spots = new List<float>();
            for (int i = 0; i < n; i++)
                spots.Add(Mathf.Lerp(boss.ArenaMin + 2f, boss.MuzzlePosition.x - 4f, n == 1 ? 0.3f : i / (float)(n - 1)));

            var bars = new List<Transform>();
            foreach (float x in spots)
            {
                var portal = Instantiate(portalPrefab, new Vector3(x, 0f, 0.2f), Quaternion.identity);
                portals.Add(portal.gameObject);
                bars.Add(portal.transform.childCount > 0 ? portal.transform.GetChild(0) : null);
            }

            float t = 0f;
            float total = generateTime / speed;
            while (t < total)
            {
                t += Time.deltaTime;
                foreach (var bar in bars)
                    if (bar != null) bar.localScale = new Vector3(t / total, 1f, 1f);
                yield return null;
            }

            for (int i = 0; i < spots.Count; i++)
            {
                var prefab = prefabs[Random.Range(0, prefabs.Length)];
                alive.Add(Instantiate(prefab, new Vector3(spots[i], 0.05f, 0f), Quaternion.identity));
            }
            ClearPortals();
            boss.SetFace(BossController.Face.Idle);
        }

        void ClearPortals()
        {
            foreach (var p in portals)
                if (p != null) Destroy(p);
            portals.Clear();
        }

        public void DespawnMinions()
        {
            foreach (var e in alive)
                if (e != null) e.Despawn();
            alive.Clear();
        }

        public override void Cancel()
        {
            ClearPortals();
            DespawnMinions();
        }
    }
}
