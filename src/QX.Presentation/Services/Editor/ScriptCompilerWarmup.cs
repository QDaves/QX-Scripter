using Qx.Presentation.Threading;
using Qx.Scripting.Hosting;

namespace Qx.Presentation.Services.Editor;

public sealed class ScriptCompilerWarmup : IEditorWarmup
{
    public void WarmUp() => Task.Run(ScriptEngine.WarmUp).Observe("scripts");
}
