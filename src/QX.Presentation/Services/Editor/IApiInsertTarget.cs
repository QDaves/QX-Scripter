namespace Qx.Presentation.Services.Editor;

public interface IApiInsertTarget
{
    void Insert(string text, int caret_offset);

    void CloseApiBrowser();
}
