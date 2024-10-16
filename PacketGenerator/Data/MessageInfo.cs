namespace PacketGenerator.Data;

public class MessageInfo
{
    public string Name { get; init; }
    public string Namespace { get; init; }
    public string Modifier { get; init; }
    public List<PacketMember> Members { get; init; }
}