using Unity.Networking.Transport;
using Unity.Collections;

public class NetAddTroopsReinforcement : NetMessage
{
    public string territoryName;
    public int troopsToAdd;
    public string playerAlias;

    public NetAddTroopsReinforcement()
    {
        Code = OpCode.ADD_TROOPS_REINFORCEMENT;
    }

    public NetAddTroopsReinforcement(DataStreamReader reader)
    {
        Code = OpCode.ADD_TROOPS_REINFORCEMENT;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        writer.WriteFixedString4096(territoryName);
        writer.WriteInt(troopsToAdd);
        writer.WriteFixedString4096(playerAlias);
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
        territoryName = reader.ReadFixedString4096().ToString();
        troopsToAdd = reader.ReadInt();
        playerAlias = reader.ReadFixedString4096().ToString();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_ADD_TROOPS_REINFORCEMENT?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        NetUtility.S_ADD_TROOPS_REINFORCEMENT?.Invoke(this, cnn);
    }
}