using Unity.Networking.Transport;
using Unity.Collections;

public class NetExchangeCards : NetMessage
{
    public string playerAlias;
    public int[] cardIds; // IDs de las cartas que se intercambian

    public NetExchangeCards()
    {
        Code = OpCode.EXCHANGE_CARDS;
    }

    public NetExchangeCards(DataStreamReader reader)
    {
        Code = OpCode.EXCHANGE_CARDS;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        writer.WriteFixedString4096(playerAlias);
        writer.WriteInt(cardIds.Length);
        for (int i = 0; i < cardIds.Length; i++)
        {
            writer.WriteInt(cardIds[i]);
        }
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
        playerAlias = reader.ReadFixedString4096().ToString();
        int length = reader.ReadInt();
        cardIds = new int[length];
        for (int i = 0; i < length; i++)
        {
            cardIds[i] = reader.ReadInt();
        }
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_EXCHANGE_CARDS?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        NetUtility.S_EXCHANGE_CARDS?.Invoke(this, cnn);
    }
}