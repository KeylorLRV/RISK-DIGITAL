using Unity.Networking.Transport;
using Unity.Collections;
using UnityEngine;

public class NetUpdateTerritoryOwner : NetMessage
{
    public string territoryName;
    public string newOwnerAlias;

    public NetUpdateTerritoryOwner()
    {
        Code = OpCode.UPDATE_TERRITORY_OWNER;
    }

    public NetUpdateTerritoryOwner(DataStreamReader reader)
    {
        Code = OpCode.UPDATE_TERRITORY_OWNER;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        writer.WriteFixedString4096(territoryName);
        writer.WriteFixedString4096(newOwnerAlias);
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
        territoryName = reader.ReadFixedString4096().ToString();
        newOwnerAlias = reader.ReadFixedString4096().ToString();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_UPDATE_TERRITORY_OWNER?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        Debug.LogWarning("Server received NetUpdateTerritoryOwner, which should only be sent to clients.");
    }
}