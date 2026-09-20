using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Game.Snapshots;
using Qx.Presentation.Services.Navigator;

namespace Qx.Presentation.ViewModels.Navigator;

public enum RoomResultField
{
    Name,
    Owner,
    Users,
    Capacity,
    Score,
    Door,
    Id
}

public sealed partial class RoomRow : ObservableObject
{
    public RoomRow(RoomDataSnapshot room)
    {
        ArgumentNullException.ThrowIfNull(room);
        Id = room.Id;
        IdText = ((long)room.Id).ToString(CultureInfo.InvariantCulture);
        Take(room);
    }

    public long Id { get; }

    public string IdText { get; }

    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    [ObservableProperty]
    public partial string Owner { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsersText))]
    public partial int Users { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CapacityText))]
    public partial int Capacity { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScoreText))]
    public partial int Score { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DoorText))]
    public partial int DoorMode { get; private set; }

    [ObservableProperty]
    public partial string? Detail { get; private set; }

    public string UsersText => NavigatorText.Number(Users);

    public string CapacityText => NavigatorText.Number(Capacity);

    public string ScoreText => NavigatorText.Number(Score);

    public string DoorText => NavigatorText.Door(DoorMode);

    public string Line => string.Join('\t', Name, Owner, UsersText, CapacityText, ScoreText, DoorText, IdText);

    public void Take(RoomDataSnapshot room)
    {
        ArgumentNullException.ThrowIfNull(room);
        Name = room.Name;
        Owner = room.OwnerName;
        Users = room.UserCount;
        Capacity = room.MaxUserCount;
        Score = room.Score;
        DoorMode = room.DoorMode;
        Detail = NavigatorText.Detail(
            room.Description,
            room.Tags,
            room.HasGroup ? room.GroupName : "",
            room.HasEvent ? room.EventName : "");
    }

    public string Value(RoomResultField field) => field switch
    {
        RoomResultField.Owner => Owner,
        RoomResultField.Users => UsersText,
        RoomResultField.Capacity => CapacityText,
        RoomResultField.Score => ScoreText,
        RoomResultField.Door => DoorText,
        RoomResultField.Id => IdText,
        _ => Name
    };
}
