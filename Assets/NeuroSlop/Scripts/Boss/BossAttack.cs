using System.Collections;
using UnityEngine;

namespace NeuroSlop
{
    /// <summary>
    /// One boss move. Contract: telegraph first, then act; Cancel() must leave no leftovers.
    /// </summary>
    public abstract class BossAttack : MonoBehaviour
    {
        [SerializeField] protected string consoleLine = "> generate()";

        public virtual bool CanRun(BossController boss) => true;
        public abstract IEnumerator Run(BossController boss, float speed);
        public virtual void Cancel() { }

        protected static void Announce(string line)
        {
            MetaMessages.Post(line, MessageKind.System);
            Sfx.Play(SfxId.Telegraph);
        }
    }
}
