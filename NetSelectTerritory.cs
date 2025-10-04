// NetSelectTerritory.cs
using Unity.Networking.Transport;
using Unity.Collections;

public class NetSelectTerritory : NetMessage
{
    public string territoryName;
    public int playerId; // O el alias del jugador

    public NetSelectTerritory()
    {
        Code = OpCode.SELECT_TERRITORY;
    }

    public NetSelectTerritory(DataStreamReader reader)
    {
        Code = OpCode.SELECT_TERRITORY;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        writer.WriteFixedString4096(territoryName); // Asumiendo que el nombre del territorio no excede 4096 caracteres
        writer.WriteInt(playerId);
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
        territoryName = reader.ReadFixedString4096().ToString();
        playerId = reader.ReadInt();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_SELECT_TERRITORY?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        NetUtility.S_SELECT_TERRITORY?.Invoke(this, cnn);
    }
}
