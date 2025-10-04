using Unity.Networking.Transport;
using Unity.Collections;

public class NetShowTieMessage : NetMessage
{
    public string message; // Texto a mostrar, por ejemplo "Empate en combate"

    public NetShowTieMessage()
    {
        Code = OpCode.SHOW_TIE_MESSAGE;
    }

    public NetShowTieMessage(DataStreamReader reader)
    {
        Code = OpCode.SHOW_TIE_MESSAGE;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        writer.WriteFixedString4096(message);
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
        message = reader.ReadFixedString4096().ToString();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_SHOW_TIE_MESSAGE?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        UnityEngine.Debug.LogWarning("Server received NetShowTieMessage, which should only be sent to clients.");
    }
}