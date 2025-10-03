using Unity.Networking.Transport;



public class NetMessage
{
    public OpCode Code { get; set; }

    public virtual void Serialize(ref Unity.Collections.DataStreamWriter writer)
    {
        writer.WriteByte((byte)Code);
    }
    public virtual void Deserialize(ref Unity.Collections.DataStreamReader reader)
    {

    }
    public virtual void ReceivedOnClient()
    {

    }
    public virtual void ReceivedOnServer(NetworkConnection cnn)
    {

    }
}