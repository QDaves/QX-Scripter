using Qx.Presentation.Visuals;

namespace Qx.Presentation.Dialogs;

public sealed record PromptRequest(string Title, string Initial, string AcceptText, string Placeholder, IconKind Icon = IconKind.Question, bool AllowEmpty = false, string? Caption = null);
