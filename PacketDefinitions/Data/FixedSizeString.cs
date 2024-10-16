namespace PacketDefinitions.Data;

public class FixedSizeString(int size = 15)
{
    public int Size { get; } = size;
    public string Value { get; set; } = string.Empty;
}