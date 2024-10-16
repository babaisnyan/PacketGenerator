namespace PacketDefinitions.Data;

[AttributeUsage(AttributeTargets.Class)]
public class SerializableMessageAttribute : Attribute
{
    public SerializableMessageAttribute(int id)
    {
        Id = id;
    }

    public SerializableMessageAttribute() { }

    public int Id { get; }
}