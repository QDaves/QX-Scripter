namespace Qx.Presentation.Services.Room;

public interface IRoomSnapshotSource
{
    RoomSnapshot Current { get; }

    event Action<RoomSnapshot>? Updated;

    void Start();

    void Stop();

    void Settle();

    void Refresh();
}
