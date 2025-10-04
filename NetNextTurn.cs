using Unity.Networking.Transport;
using Unity.Collections;

public class NetNextTurn : NetMessage
{
    public string playerAlias;
    public int fase;
    public int roundCounter;

    public NetNextTurn()
    {
        Code = OpCode.NEXT_TURN;
    }

    public NetNextTurn(DataStreamReader reader)
    {
        Code = OpCode.NEXT_TURN;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        writer.WriteFixedString4096(playerAlias);
        writer.WriteInt(fase);
        writer.WriteInt(roundCounter);
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
        playerAlias = reader.ReadFixedString4096().ToString();
        fase = reader.ReadInt();
        roundCounter = reader.ReadInt();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_NEXT_TURN?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        NetUtility.S_NEXT_TURN?.Invoke(this, cnn);
    }
}
