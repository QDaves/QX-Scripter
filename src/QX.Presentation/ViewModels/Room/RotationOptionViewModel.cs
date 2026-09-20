using System.Windows.Input;

namespace Qx.Presentation.ViewModels.Room;

public sealed record RotationOptionViewModel(int Direction, string Name, ICommand? Turn, bool CanPick)
{
    public double Angle => Direction * 45d;

    public bool HasDirection => Direction >= 0;
}
