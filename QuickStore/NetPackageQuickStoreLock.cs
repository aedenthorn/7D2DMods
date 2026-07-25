using System;

namespace QuickStorage
{
    internal class NetPackageQuickStoreLock : NetPackage
    {
        public ILockTarget[] targets;
        public bool unlock;

        public NetPackageQuickStoreLock Setup(ILockTarget[] _targets, bool _unlock)
        {
            targets = _targets;
            unlock = _unlock;
            return this;
        }
        public override void read(PooledBinaryReader _br)
        {
            int num = _br.ReadInt32();
            if (num > 0)
            {
                targets = new ILockTarget[num];
                for (int i = 0; i < num; i++)
                {
                    targets[i] = ILockTarget.ReadIdentifyingInfo(_br);
                }
            }
            unlock = _br.ReadBoolean();
        }

        public override void write(PooledBinaryWriter _bw)
        {
            base.write(_bw);
            ILockTarget[] array = this.targets;
            _bw.Write((array != null) ? array.Length : 0);
            if (targets != null)
            {
                for (int i = 0; i < this.targets.Length; i++)
                {
                    ILockTarget.WriteIdentifyingInfo(this.targets[i], _bw);
                }
            }
            _bw.Write(unlock);
        }
        public override int GetLength()
        {
            return 0;
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (!QuickStorage.config.modEnabled || SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer || targets is null)
                return;
            if (!unlock)
            {
                QuickStorage.Dbgl($"received locked message");
                foreach(var target in targets)
                {
                    QuickStorage.lockedList.Add(target);
                }
            }
            else
            {
                QuickStorage.Dbgl($"received unlocked message");
                foreach (var target in targets)
                {
                    QuickStorage.lockedList.Remove(target);
                }
            }
        }
    }
}