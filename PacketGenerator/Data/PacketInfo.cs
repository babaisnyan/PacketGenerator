namespace PacketGenerator.Data;

public class PacketInfo : MessageInfo
{
    public string Prefix { get; init; }
    public bool IsFromClient { get; init; }
    public int Type { get; init; }
   
}