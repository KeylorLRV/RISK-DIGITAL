using Unity.Networking.Transport;
using Unity.Collections;

public class NetSyncCurrentGameState : NetMessage
{
    public string currentPlayerAlias;
    public int currentPhase;
    public int currentRound;

    public NetSyncCurrentGameState()
    {
        Code = OpCode.SYNC_CURRENT_GAME_STATE;
    }

    public NetSyncCurrentGameState(DataStreamReader reader)
    {
        Code = OpCode.SYNC_CURRENT_GAME_STATE;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        writer.WriteFixedString4096(currentPlayerAlias);
        writer.WriteInt(currentPhase);
        writer.WriteInt(currentRound);
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
        currentPlayerAlias = reader.ReadFixedString4096().ToString();
        currentPhase = reader.ReadInt();
        currentRound = reader.ReadInt();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_SYNC_CURRENT_GAME_STATE?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        UnityEngine.Debug.LogWarning("Server received NetSyncCurrentGameState, which should only be sent to clients.");
    }
}