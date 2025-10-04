// NetResolveCombat.cs
using Unity.Networking.Transport;
using Unity.Collections;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetResolveCombat : NetMessage
{
    public string attackerTerritoryName;
    public string defenderTerritoryName;
    public int attackerDice1, attackerDice2, attackerDice3; // Resultados de los dados del atacante
    public int defenderDice1, defenderDice2; // Resultados de los dados del defensor
    public bool defenderLostTroop;
    public bool attackerLostTroop;
    public bool territoryConquered;
    public string newOwnerAlias; // Si fue conquistado
    public int troopsMovedAfterConquest; // Si fue conquistado

    public NetResolveCombat()
    {
        Code = OpCode.RESOLVE_COMBAT;
    }

    public NetResolveCombat(DataStreamReader reader)
    {
        Code = OpCode.RESOLVE_COMBAT;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        writer.WriteFixedString4096(attackerTerritoryName);
        writer.WriteFixedString4096(defenderTerritoryName);
        writer.WriteInt(attackerDice1);
        writer.WriteInt(attackerDice2);
        writer.WriteInt(attackerDice3);
        writer.WriteInt(defenderDice1);
        writer.WriteInt(defenderDice2);
        writer.WriteByte(defenderLostTroop ? (byte)1 : (byte)0);
        writer.WriteByte(attackerLostTroop ? (byte)1 : (byte)0);
        writer.WriteByte(territoryConquered ? (byte)1 : (byte)0);
        writer.WriteFixedString4096(newOwnerAlias);
        writer.WriteInt(troopsMovedAfterConquest);
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
        attackerTerritoryName = reader.ReadFixedString4096().ToString();
        defenderTerritoryName = reader.ReadFixedString4096().ToString();
        attackerDice1 = reader.ReadInt();
        attackerDice2 = reader.ReadInt();
        attackerDice3 = reader.ReadInt();
        defenderDice1 = reader.ReadInt();
        defenderDice2 = reader.ReadInt();
        defenderLostTroop = reader.ReadByte() == 1;
        attackerLostTroop = reader.ReadByte() == 1;
        territoryConquered = reader.ReadByte() == 1;
        newOwnerAlias = reader.ReadFixedString4096().ToString();
        troopsMovedAfterConquest = reader.ReadInt();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_RESOLVE_COMBAT?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        // El servidor no debería recibir este mensaje, solo enviarlo.
        Debug.LogWarning("Server received NetResolveCombat, which should only be sent to clients.");
    }
}
