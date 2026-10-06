using System;

namespace QuickStorage
{
    internal class NetPackageQuickStoreLock : NetPackage
    {
        public PooledExpandableMemoryStream payload;

        public NetPackageQuickStoreLock Setup(PooledExpandableMemoryStream _payload)
        {
            this.payload = MemoryPools.poolMemoryStream.AllocSync(true);
            _payload.WriteTo(this.payload);
            return this;
        }
        public override void read(PooledBinaryReader _br)
        {
            this.payload = MemoryPools.poolMemoryStream.AllocSync(true);
            int num = _br.ReadInt32();
            StreamUtils.StreamCopy(_br.BaseStream, this.payload, num, null, true);
        }

        public override void write(PooledBinaryWriter _bw)
        {
            base.write(_bw);
            _bw.Write((int)this.payload.Length);
            this.payload.WriteTo(_bw.BaseStream);
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (!QuickStorage.config.modEnabled || SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                return;

            using (PooledBinaryReader pooledBinaryReader = MemoryPools.poolBinaryReader.AllocSync(false))
            {
                PooledExpandableMemoryStream pooledExpandableMemoryStream = this.payload;
                lock (pooledExpandableMemoryStream)
                {
                    pooledBinaryReader.SetBaseStream(this.payload);
                    this.payload.Position = 0L;
                    var locking = pooledBinaryReader.ReadBoolean();
                    ushort channel = pooledBinaryReader.ReadUInt16();
                    int length = pooledBinaryReader.ReadInt32();
                    if (length < 1 || length > 5)
                        return;
                    ILockTarget[] targets = new ILockTarget[length];
                    for (int i = 0; i < length; i++)
                    {
                        targets[i] = WorldAddress.Read(pooledBinaryReader).Resolve<ILockTarget>();
                    }
                    if (locking)
                    {
                        QuickStorage.Dbgl($"received locked message");
                        foreach (var target in targets)
                        {
                            if (target != null)
                                QuickStorage.lockedList.Add(target);
                        }
                    }
                    else
                    {
                        QuickStorage.Dbgl($"received unlocked message");
                        foreach (var target in targets)
                        {
                            if (target != null)
                                QuickStorage.lockedList.Remove(target);
                        }
                    }
                }
            }
        }
    }
}