using Unity.Networking.Transport;
using Unity.Collections;

public class NetHideDiceRoll : NetMessage
{
    public NetHideDiceRoll()
    {
        Code = OpCode.HIDE_DICE_ROLL;
    }

    public NetHideDiceRoll(DataStreamReader reader)
    {
        Code = OpCode.HIDE_DICE_ROLL;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        // No datos adicionales
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_HIDE_DICE_ROLL?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        UnityEngine.Debug.LogWarning("Server received NetHideDiceRoll, which should only be sent to clients.");
    }
}