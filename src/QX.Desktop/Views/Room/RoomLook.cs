using Avalonia.Data.Converters;
using Qx.Desktop.Controls;
using Qx.Presentation.Services.Room;

namespace Qx.Desktop.Views.Room;

public static class RoomLook
{
    public static FuncValueConverter<RoomPersonKind, AvatarKind> PersonKind { get; } =
        new(kind => kind switch
        {
            RoomPersonKind.Bot => AvatarKind.Bot,
            RoomPersonKind.Pet => AvatarKind.Pet,
            _ => AvatarKind.Person
        });
}
