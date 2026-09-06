namespace Qx.Messages;

public interface IParser<T> where T : IParser<T>
{
    static abstract T Parse(in PacketReader p);
}
