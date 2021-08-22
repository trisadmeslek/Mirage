// DO NOT EDIT: GENERATED BY BitCountTestGenerator.cs

using System;
using System.Collections;
using Mirage.Serialization;
using Mirage.Tests.Runtime.ClientServer;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Mirage.Tests.Runtime.Generated.BitCountAttributeTests
{
    [System.Flags, System.Serializable]
    public enum MyEnum
    {
        None = 0,
        HasHealth = 1,
        HasArmor = 2,
        HasGun = 4,
        HasAmmo = 8,
    }
    public class BitCountBehaviour_MyEnum_4 : NetworkBehaviour
    {
        [BitCount(4)]
        [SyncVar] public MyEnum myValue;

        public event Action<MyEnum> onRpc;

        [ClientRpc]
        public void RpcSomeFunction([BitCount(4)] MyEnum myParam)
        {
            onRpc?.Invoke(myParam);
        }
    }
    public class BitCountTest_MyEnum_4 : ClientServerSetup<BitCountBehaviour_MyEnum_4>
    {
        [Test]
        public void SyncVarIsBitPacked()
        {
            var behaviour = new BitCountBehaviour_MyEnum_4();

            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                behaviour.SerializeSyncVars(writer, true);

                Assert.That(writer.BitPosition, Is.EqualTo(4));
            }
        }

        // [UnityTest]
        // [Ignore("Rpc not supported yet")]
        public IEnumerator RpcIsBitPacked()
        {
            const MyEnum value = (MyEnum)3;

            int called = 0;
            clientComponent.onRpc += (v) => { called++; Assert.That(v, Is.EqualTo(value)); };

            client.MessageHandler.UnregisterHandler<RpcMessage>();
            int payloadSize = 0;
            client.MessageHandler.RegisterHandler<RpcMessage>((player, msg) =>
            {
                // store value in variable because assert will throw and be catch by message wrapper
                payloadSize = msg.payload.Count;
                clientObjectManager.OnRpcMessage(msg);
            });


            serverComponent.RpcSomeFunction(value);
            yield return null;
            Assert.That(called, Is.EqualTo(1));
            Assert.That(payloadSize, Is.EqualTo(1), $"4 bits is 1 bytes in payload");
        }
    }
}