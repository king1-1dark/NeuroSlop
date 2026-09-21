using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeuroSlop
{
    /// <summary>Layer lookups, resolved by name so nothing depends on hard-coded indices.</summary>
    public static class Layers
    {
        public static int Ground => LayerMask.NameToLayer("Ground");
        public static int Player => LayerMask.NameToLayer("Player");
        public static int Enemy => LayerMask.NameToLayer("Enemy");
        public static int GroundMask => LayerMask.GetMask("Ground");
        public static int HurtMask => LayerMask.GetMask("Player", "Enemy");

        // Characters overlap freely (classic beat'em up), they only collide with the world.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ConfigureCollisions()
        {
            if (Player < 0 || Enemy < 0) { Debug.LogError("NeuroSlop: layers 'Player'/'Enemy' are missing. Run NeuroSlop/Build MVP."); return; }
            Physics2D.IgnoreLayerCollision(Player, Enemy, true);
            Physics2D.IgnoreLayerCollision(Enemy, Enemy, true);
            Physics2D.IgnoreLayerCollision(Player, Player, true);
        }
    }

    /// <summary>
    /// Every attack in the game goes through here: box query → Health.TakeDamage → Landed event.
    /// FX / HUD listen to Landed, so attacks never talk to feedback code directly.
    /// </summary>
    public static class Hits
    {
        public static event Action<Health, DamageInfo, Team> Landed;

        static readonly List<Collider2D> buffer = new();
        static readonly HashSet<Health> scratch = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Landed = null;

        public static int Box(Vector2 center, Vector2 size, Team attacker, DamageInfo info, HashSet<Health> alreadyHit = null)
        {
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = Layers.HurtMask };
            buffer.Clear();
            Physics2D.OverlapBox(center, size, 0f, filter, buffer);

            var seen = alreadyHit ?? scratch;
            if (alreadyHit == null) scratch.Clear();

            int count = 0;
            foreach (var col in buffer)
            {
                var target = col.GetComponentInParent<Health>();
                if (target == null || target.Team == attacker || target.IsDead) continue;
                if (!seen.Add(target)) continue;

                var hit = info;
                hit.Point = col.ClosestPoint(center);
                if (target.TakeDamage(hit))
                {
                    count++;
                    Landed?.Invoke(target, hit, attacker);
                }
            }
            return count;
        }
    }
}
