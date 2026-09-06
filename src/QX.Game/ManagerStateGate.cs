namespace Qx.Game;

internal sealed class ManagerStateGate
{
    private readonly object _publication_sync = new();
    private readonly object _state_sync = new();
    private long _generation;
    private long _reset_generation = -1;

    public TResult Capture<TResult>(Func<TResult> projection)
    {
        lock (_state_sync)
            return projection();
    }

    public bool Commit(
        long generation,
        Action mutation,
        Action publication)
    {
        lock (_publication_sync)
        {
            lock (_state_sync)
            {
                if (generation < _generation)
                    return false;
                if (generation > _generation)
                    _generation = generation;
                _reset_generation = -1;
                mutation();
            }
        }
        if (!CanPublish(generation))
            return true;
        publication();
        return true;
    }

    public bool Reset(
        long generation,
        Action mutation,
        Action publication)
    {
        lock (_publication_sync)
        {
            lock (_state_sync)
            {
                if (generation < _generation ||
                    generation == _reset_generation)
                {
                    return false;
                }
                _generation = generation;
                mutation();
                _reset_generation = generation;
            }
        }
        try
        {
            publication();
        }
        catch
        {
            lock (_publication_sync)
            {
                lock (_state_sync)
                {
                    if (_reset_generation == generation)
                        _reset_generation = -1;
                }
            }
            throw;
        }
        return true;
    }

    private bool CanPublish(long generation)
    {
        lock (_publication_sync)
        {
            lock (_state_sync)
            {
                return generation == _generation &&
                    generation != _reset_generation;
            }
        }
    }
}
