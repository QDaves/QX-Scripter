using Avalonia.Controls;

namespace Qx.Desktop.Services;

public sealed class TopLevelAccessor
{
    TopLevel? _top;

    public TopLevel? Current => Volatile.Read(ref _top);

    public void Attach(TopLevel top)
    {
        ArgumentNullException.ThrowIfNull(top);
        Volatile.Write(ref _top, top);
    }
}
