namespace Qx.Presentation.Input;

public interface ICommandPalette
{
    bool IsOpen { get; }

    void Open();

    void Dismiss();
}
