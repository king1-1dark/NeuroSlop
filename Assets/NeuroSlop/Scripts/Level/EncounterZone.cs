using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NeuroSlop
{
    /// <summary>
    /// A comic "panel": when the player walks in, the camera locks to [minX, maxX] and waves
    /// of enemies are generated (or the boss wakes up). Cleared → camera unlocks.
    /// </summary>
    public class EncounterZone : MonoBehaviour
    {
        [Serializable]
        public class Spawn
        {
            public EnemyController prefab;
            [Tooltip("Offset from the zone's minX.")] public float x;
            public float delay;
        }

        [Serializable]
        public class Wave
        {
            public Spawn[] spawns;
        }

        [SerializeField] float minX;
        [SerializeField] float maxX = 18f;
        [SerializeField] float triggerOffset = 2f;
        [SerializeField] Wave[] waves;
        [SerializeField] BossController boss;
        [SerializeField] float corruptionOnEnter = 0.05f;
        [SerializeField, TextArea] string enterLine;

        readonly List<EnemyController> alive = new();
        Coroutine routine;
        bool firstEntry = true;
        bool bossDefeated;

        public float MinX => minX;
        public float MaxX => maxX;
        public bool IsBoss => boss != null;
        public bool Running { get; private set; }
        public bool Cleared { get; private set; }
        public event Action<EncounterZone> Started;
        public event Action<EncounterZone> Completed;

        void Awake()
        {
            if (boss != null) boss.Defeated += () => bossDefeated = true;
        }

        void Update()
        {
            if (Cleared || Running) return;
            var player = PlayerController.Instance;
            if (player != null && !player.IsDead && player.transform.position.x >= minX + triggerOffset) Begin();
        }

        void Begin()
        {
            Running = true;
            if (firstEntry)
            {
                firstEntry = false;
                CorruptionSystem.Add(corruptionOnEnter);
                GenerationCostService.Register(GenerationKind.Scene);
                if (!string.IsNullOrEmpty(enterLine)) MetaMessages.Post(enterLine);
            }
            Started?.Invoke(this);
            routine = StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            if (boss != null)
            {
                boss.Activate(minX, maxX);
                while (!bossDefeated) yield return null;
            }
            else if (waves != null)
            {
                foreach (var wave in waves)
                {
                    foreach (var spawn in wave.spawns)
                    {
                        if (spawn.delay > 0f) yield return new WaitForSeconds(spawn.delay);
                        var enemy = Instantiate(spawn.prefab, new Vector3(minX + spawn.x, 0.05f, 0f), Quaternion.identity);
                        enemy.Died += OnEnemyDied;
                        alive.Add(enemy);
                    }
                    while (alive.Count > 0) yield return null;
                    yield return new WaitForSeconds(0.5f);
                }
            }
            Running = false;
            Cleared = true;
            Completed?.Invoke(this);
        }

        void OnEnemyDied(EnemyController enemy) => alive.Remove(enemy);

        /// <summary>Player died inside: wipe the panel and let it trigger again.</summary>
        public void ResetForRespawn()
        {
            if (Cleared) return;
            if (routine != null) StopCoroutine(routine);
            routine = null;
            foreach (var e in alive)
                if (e != null) e.Despawn();
            alive.Clear();
            Running = false;
            if (boss != null) boss.ResetForRespawn();
        }

        void OnDrawGizmos()
        {
            Gizmos.color = IsBoss ? Color.red : Color.yellow;
            Gizmos.DrawLine(new Vector3(minX, -1f), new Vector3(minX, 9f));
            Gizmos.DrawLine(new Vector3(maxX, -1f), new Vector3(maxX, 9f));
        }
    }
}
