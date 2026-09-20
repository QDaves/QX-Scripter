namespace Qx.Presentation.Platform;

public interface IKeyboardState
{
    bool IsSupported { get; }

    bool IsShiftDown();
}
