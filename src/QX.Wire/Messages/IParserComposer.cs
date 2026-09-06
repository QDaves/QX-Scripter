namespace Qx.Messages;

public interface IParserComposer<T> : IComposer, IParser<T> where T : IParser<T>;
