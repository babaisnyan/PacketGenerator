namespace PacketGenerator.Data;

public class PacketMember
{
    public string Name { get; set; }
    public string CppName { get; set; }
    public string TypeName { get; set; }
    public string CppTypeName { get; set; }
    public uint TypeId { get; set; }
    public bool IsCollection { get; set; }
    public string GenericTypeName { get; set; } = string.Empty;
    public string[] GenericTypeNames { get; set; } = [];
    public bool IsNullable { get; set; }
    public string CollectionSizeType { get; set; } = "uint16_t";
    public bool IsFixedString { get; set; }
}