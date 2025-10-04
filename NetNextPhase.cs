using Unity.Networking.Transport;
using Unity.Collections;

public class NetNextPhase : NetMessage
{
    public int newPhase;

    public NetNextPhase()
    {
        Code = OpCode.NEXT_PHASE;
    }

    public NetNextPhase(DataStreamReader reader)
    {
        Code = OpCode.NEXT_PHASE;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        writer.WriteInt(newPhase);
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
        newPhase = reader.ReadInt();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_NEXT_PHASE?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        NetUtility.S_NEXT_PHASE?.Invoke(this, cnn);
    }
}