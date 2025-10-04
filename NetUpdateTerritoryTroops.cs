using Unity.Networking.Transport;
using Unity.Collections;
using UnityEngine;

public class NetUpdateTerritoryTroops : NetMessage
{
    public string territoryName;
    public int troopsCount;

    public NetUpdateTerritoryTroops()
    {
        Code = OpCode.UPDATE_TERRITORY_TROOPS;
    }

    public NetUpdateTerritoryTroops(DataStreamReader reader)
    {
        Code = OpCode.UPDATE_TERRITORY_TROOPS;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        writer.WriteFixedString4096(territoryName);
        writer.WriteInt(troopsCount);
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
        territoryName = reader.ReadFixedString4096().ToString();
        troopsCount = reader.ReadInt();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_UPDATE_TERRITORY_TROOPS?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        Debug.LogWarning("Server received NetUpdateTerritoryTroops, which should only be sent to clients.");
    }
}