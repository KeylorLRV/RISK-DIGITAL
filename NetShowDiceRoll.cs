using Unity.Networking.Transport;
using Unity.Collections;

public class NetShowDiceRoll : NetMessage
{
    public int[] attackerDice;
    public int[] defenderDice;

    public NetShowDiceRoll()
    {
        Code = OpCode.SHOW_DICE_ROLL;
    }

    public NetShowDiceRoll(DataStreamReader reader)
    {
        Code = OpCode.SHOW_DICE_ROLL;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);

        writer.WriteInt(attackerDice.Length);
        for (int i = 0; i < attackerDice.Length; i++)
            writer.WriteInt(attackerDice[i]);

        writer.WriteInt(defenderDice.Length);
        for (int i = 0; i < defenderDice.Length; i++)
            writer.WriteInt(defenderDice[i]);
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);

        int attackerCount = reader.ReadInt();
        attackerDice = new int[attackerCount];
        for (int i = 0; i < attackerCount; i++)
            attackerDice[i] = reader.ReadInt();

        int defenderCount = reader.ReadInt();
        defenderDice = new int[defenderCount];
        for (int i = 0; i < defenderCount; i++)
            defenderDice[i] = reader.ReadInt();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_SHOW_DICE_ROLL?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        UnityEngine.Debug.LogWarning("Server received NetShowDiceRoll, which should only be sent to clients.");
    }
}