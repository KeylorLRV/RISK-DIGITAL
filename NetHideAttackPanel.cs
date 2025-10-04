using Unity.Networking.Transport;
using Unity.Collections;

public class NetHideAttackPanel : NetMessage
{
    public NetHideAttackPanel()
    {
        Code = OpCode.HIDE_ATTACK_PANEL;
    }

    public NetHideAttackPanel(DataStreamReader reader)
    {
        Code = OpCode.HIDE_ATTACK_PANEL;
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
        NetUtility.C_HIDE_ATTACK_PANEL?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        UnityEngine.Debug.LogWarning("Server received NetHideAttackPanel, which should only be sent to clients.");
    }
}