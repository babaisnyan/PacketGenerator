namespace PacketDefinitions.Data;

[AttributeUsage(AttributeTargets.Property)]
public class SerializableCollectionAttribute(int lengthSize) : Attribute
{
    public int LengthSize { get; } = lengthSize;
}