using System;

namespace QuickStorage
{
    internal class NetPackageQuickStoreLock : NetPackage
    {
        public int posX;
        public int posY;
        public int posZ;
        public bool unlock;

        public NetPackageQuickStoreLock Setup(Vector3i _pos, bool _unlock)
        {
            posX = _pos.x;
            posY = _pos.y;
            posZ = _pos.z;
            unlock = _unlock;
            return this;
        }
        public override void read(PooledBinaryReader _br)
        {
            posX = _br.ReadInt32();
            posY = _br.ReadInt32();
            posZ = _br.ReadInt32();
            unlock = _br.ReadBoolean();
        }

        public override void write(PooledBinaryWriter _bw)
        {
            base.write(_bw);
            _bw.Write(posX);
            _bw.Write(posY);
            _bw.Write(posZ);
            _bw.Write(unlock);
        }
        public override int GetLength()
        {
            return 0;
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (!QuickStorage.config.modEnabled || SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                return;
            if (!unlock)
            {
                QuickStorage.Dbgl($"received locked message");
                QuickStorage.lockedList.Add(new Vector3i(posX, posY, posZ));
            }
            else
            {
                QuickStorage.Dbgl($"received unlocked message");
                QuickStorage.lockedList.Remove(new Vector3i(posX, posY, posZ));
            }
        }
    }
}