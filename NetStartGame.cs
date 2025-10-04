using Unity.Networking.Transport;
using Unity.Collections;

public class NetStartGame : NetMessage
{
    public NetStartGame()
    {
        Code = OpCode.START_GAME;
    }

    public NetStartGame(DataStreamReader reader)
    {
        Code = OpCode.START_GAME;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        // No datos adicionales por ahora
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_START_GAME?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        NetUtility.S_START_GAME?.Invoke(this, cnn);
    }
}
