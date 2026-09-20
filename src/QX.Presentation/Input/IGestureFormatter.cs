namespace Qx.Presentation.Input;

public interface IGestureFormatter
{
    bool PrimaryIsControl { get; }

    string Describe(KeyChord chord);
}
