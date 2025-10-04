using Unity.Networking.Transport;
using Unity.Collections;

public class NetDrawCard : NetMessage
{
    public string playerAlias;
    public int cardId; // Identificador de la carta

    public NetDrawCard()
    {
        Code = OpCode.DRAW_CARD;
    }

    public NetDrawCard(DataStreamReader reader)
    {
        Code = OpCode.DRAW_CARD;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        writer.WriteFixedString4096(playerAlias);
        writer.WriteInt(cardId);
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
        playerAlias = reader.ReadFixedString4096().ToString();
        cardId = reader.ReadInt();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_DRAW_CARD?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        NetUtility.S_DRAW_CARD?.Invoke(this, cnn);
    }
}