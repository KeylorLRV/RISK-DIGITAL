using Unity.Networking.Transport;
using Unity.Collections;

public class NetShowAttackPanel : NetMessage
{
    public string attackerTerritory;
    public string defenderTerritory;

    public NetShowAttackPanel()
    {
        Code = OpCode.SHOW_ATTACK_PANEL;
    }

    public NetShowAttackPanel(DataStreamReader reader)
    {
        Code = OpCode.SHOW_ATTACK_PANEL;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        writer.WriteFixedString4096(attackerTerritory);
        writer.WriteFixedString4096(defenderTerritory);
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
        attackerTerritory = reader.ReadFixedString4096().ToString();
        defenderTerritory = reader.ReadFixedString4096().ToString();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_SHOW_ATTACK_PANEL?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        // Solo servidor envía este mensaje
        UnityEngine.Debug.LogWarning("Server received NetShowAttackPanel, which should only be sent to clients.");
    }
}