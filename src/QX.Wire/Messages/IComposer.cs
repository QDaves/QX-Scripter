namespace Qx.Messages;

public interface IComposer
{
    void Compose(in PacketWriter p);
}
