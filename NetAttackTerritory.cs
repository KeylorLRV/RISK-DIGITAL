// NetAttackTerritory.cs
using Unity.Networking.Transport;
using Unity.Collections;

public class NetAttackTerritory : NetMessage
{
    public string attackerTerritoryName;
    public string defenderTerritoryName;
    public int attackingTroops;
    public int defendingTroops; // Tropas que el defensor decide usar

    public NetAttackTerritory()
    {
        Code = OpCode.ATTACK_TERRITORY;
    }

    public NetAttackTerritory(DataStreamReader reader)
    {
        Code = OpCode.ATTACK_TERRITORY;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        writer.WriteFixedString4096(attackerTerritoryName);
        writer.WriteFixedString4096(defenderTerritoryName);
        writer.WriteInt(attackingTroops);
        writer.WriteInt(defendingTroops);
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
        attackerTerritoryName = reader.ReadFixedString4096().ToString();
        defenderTerritoryName = reader.ReadFixedString4096().ToString();
        attackingTroops = reader.ReadInt();
        defendingTroops = reader.ReadInt();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_ATTACK_TERRITORY?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        NetUtility.S_ATTACK_TERRITORY?.Invoke(this, cnn);
    }
}
