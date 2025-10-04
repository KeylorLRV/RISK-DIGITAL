using Unity.Networking.Transport;
using Unity.Collections;

public class NetMoveTroops : NetMessage
{
    public string originTerritory;
    public string destinationTerritory;
    public int troopsToMove;
    public string playerAlias;

    public NetMoveTroops()
    {
        Code = OpCode.MOVE_TROOPS;
    }

    public NetMoveTroops(DataStreamReader reader)
    {
        Code = OpCode.MOVE_TROOPS;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        writer.WriteFixedString4096(originTerritory);
        writer.WriteFixedString4096(destinationTerritory);
        writer.WriteInt(troopsToMove);
        writer.WriteFixedString4096(playerAlias);
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
        originTerritory = reader.ReadFixedString4096().ToString();
        destinationTerritory = reader.ReadFixedString4096().ToString();
        troopsToMove = reader.ReadInt();
        playerAlias = reader.ReadFixedString4096().ToString();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_MOVE_TROOPS?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        NetUtility.S_MOVE_TROOPS?.Invoke(this, cnn);
    }
}