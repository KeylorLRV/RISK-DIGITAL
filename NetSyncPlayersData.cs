using Unity.Networking.Transport;
using Unity.Collections;
using UnityEngine;



public class NetSyncPlayersData : NetMessage
{
    public JugadorSaveData[] allPlayersData;

    public NetSyncPlayersData()
    {
        Code = OpCode.SYNC_PLAYERS_DATA;
    }

    public NetSyncPlayersData(DataStreamReader reader)
    {
        Code = OpCode.SYNC_PLAYERS_DATA;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        writer.WriteInt(allPlayersData.Length);
        for (int i = 0; i < allPlayersData.Length; i++)
        {
            writer.WriteFixedString64(allPlayersData[i].alias);
            writer.WriteFloat(allPlayersData[i].colorR);
            writer.WriteFloat(allPlayersData[i].colorG);
            writer.WriteFloat(allPlayersData[i].colorB);
            writer.WriteFloat(allPlayersData[i].colorA);
        }
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
        int count = reader.ReadInt();
        allPlayersData = new JugadorSaveData[count];
        for (int i = 0; i < count; i++)
        {
            allPlayersData[i] = new JugadorSaveData
            {
                alias = reader.ReadFixedString64().ToString(),
                colorR = reader.ReadByte(),
                colorG = reader.ReadByte(),
                colorB = reader.ReadByte(),
                colorA = reader.ReadByte()
            };
        }
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_SYNC_PLAYERS_DATA?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        Debug.LogWarning("Server received NetSyncPlayersData, which should only be sent to clients.");
    }
}
