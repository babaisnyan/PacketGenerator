using PacketDefinitions.Data;

namespace PacketDefinitions.Packets;

[SerializableMessage]
public class Room
{
    public int Id { get; set; }
    public string Name { get; set; }

    [SerializableCollection(1)]
    public List<FixedSizeString> Users { get; set; }
}

[SerializableMessage(3)]
public class CsRoomListReq { }

[SerializableMessage(4)]
public class ScRoomListRes
{
    public List<Room> Rooms { get; set; }
}

[SerializableMessage(5)]
public class CsRoomCreateReq
{
    public string Name { get; set; }
}

[SerializableMessage(6)]
public class ScRoomCreate
{
    public byte Result { get; set; }
    public int Id { get; set; }
    public string Name { get; set; }
}

[SerializableMessage(7)]
public class CsRoomEnterReq { }

[SerializableMessage(8)]
public class ScRoomEnterRes
{
    public byte Result { get; set; }
    public int? Id { get; set; }
    public string? Name { get; set; }

    [SerializableCollection(1)]
    public List<Tuple<FixedSizeString, int>>? Users { get; set; }
}

[SerializableMessage(9)]
public class CsChatReq
{
    public string Message { get; set; }
}

[SerializableMessage(10)]
public class ScChatRes
{
    public int SenderId { get; set; }
    public string Message { get; set; }
}

[SerializableMessage(11)]
public class CsLeaveReq { }

[SerializableMessage(12)]
public class ScLeaveRes
{
    public int Id { get; set; }
}

[SerializableMessage(13)]
public class ScRoomDelete
{
    public int Id { get; set; }
}

[SerializableMessage(14)]
public class ScRoomUserEnter
{
    public FixedSizeString Username { get; set; }
    public int Id { get; set; }
}