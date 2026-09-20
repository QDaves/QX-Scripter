using Qx.Presentation.Input;

namespace Qx.Presentation.Platform;

public interface IGlobalHotkeys
{
    bool IsSupported { get; }

    Task<IDisposable?> TryRegisterAsync(KeyChord chord, Action pressed, CancellationToken cancellation_token = default);
}
