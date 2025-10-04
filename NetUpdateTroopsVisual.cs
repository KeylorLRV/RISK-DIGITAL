using Unity.Networking.Transport;
using Unity.Collections;
using UnityEngine;

public class NetUpdateTroopsVisual : NetMessage
{
    public FixedString128Bytes territoryName;
    public int troopsCount;
    public FixedString128Bytes playerAlias;

    public NetUpdateTroopsVisual()
    {
        Code = OpCode.UPDATE_TROOPS_VISUAL;
        territoryName = default;
        troopsCount = 0;
        playerAlias = default;
    }

    public NetUpdateTroopsVisual(DataStreamReader reader)
    {
        Code = OpCode.UPDATE_TROOPS_VISUAL;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        writer.WriteFixedString128(territoryName);
        writer.WriteInt(troopsCount);
        writer.WriteFixedString128(playerAlias);
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
        territoryName = reader.ReadFixedString128();
        troopsCount = reader.ReadInt();
        playerAlias = reader.ReadFixedString128();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_UPDATE_TROOPS_VISUAL?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        // Solo servidor envía este mensaje
        Debug.LogWarning("Server received NetUpdateTroopsVisual, which should only be sent to clients.");
    }
}
