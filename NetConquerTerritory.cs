using Unity.Networking.Transport;
using Unity.Collections;
using UnityEngine;

public class NetConquerTerritory : NetMessage
{
    public string territoryName;
    public string newOwnerAlias;
    public int troopsMoved;

    public NetConquerTerritory()
    {
        Code = OpCode.CONQUER_TERRITORY;
    }

    public NetConquerTerritory(DataStreamReader reader)
    {
        Code = OpCode.CONQUER_TERRITORY;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        writer.WriteFixedString4096(territoryName);
        writer.WriteFixedString4096(newOwnerAlias);
        writer.WriteInt(troopsMoved);
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
        territoryName = reader.ReadFixedString4096().ToString();
        newOwnerAlias = reader.ReadFixedString4096().ToString();
        troopsMoved = reader.ReadInt();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_CONQUER_TERRITORY?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        Debug.LogWarning("Server received NetConquerTerritory, which should only be sent to clients.");
    }
}