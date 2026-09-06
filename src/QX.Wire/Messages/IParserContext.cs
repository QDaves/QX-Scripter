namespace Qx.Messages;

public interface IParserContext
{
    IMessageManager Messages { get; }
    MessageWireProfile WireProfile { get; }
}
