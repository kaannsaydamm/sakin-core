namespace Sakin.Messaging.Serialization
{
    public interface IMessageSerializer
    {
        byte[] Serialize<T>(T value);
        T? Deserialize<T>(byte[] data);
    }
}
