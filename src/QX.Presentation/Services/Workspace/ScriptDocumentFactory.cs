using Qx.Presentation.Platform;
using Qx.Presentation.Runtime;
using Qx.Presentation.Services.Files;
using Qx.Presentation.Services.Library;
using Qx.Presentation.Services.Output;
using Qx.Presentation.Services.Panels;
using Qx.Presentation.Services.Runs;
using Qx.Presentation.Threading;
using Qx.Presentation.ViewModels.Editor;

namespace Qx.Presentation.Services.Workspace;

public sealed class ScriptDocumentFactory(
    DesktopRuntime runtime,
    IScriptPrompts prompts,
    IScriptFileService files,
    IScriptLibrary library,
    IClipboardService clipboard,
    IUiDispatcher dispatcher,
    TimeProvider time,
    AppLifetime lifetime)
{
    public ScriptDocument Create(string name, string code, string? path) =>
        new(name, code, path, dispatcher, time, document =>
        {
            var output = new OutputBuffer(dispatcher, time);
            var panel = new PanelDocument(document, prompts, clipboard, dispatcher, time);
            var hooks = new DocumentRunHooks(document, files, library);
            var run = new ScriptRunController(document, runtime.Scripts, dispatcher, time, hooks, panel, output, lifetime.Token);
            var console = new OutputConsoleViewModel(output, clipboard, dispatcher, time);
            panel.ButtonPressed += button => PanelPress.Route(run, button);
            panel.Warned += text => output.Write(text, OutputLevel.Warning);
            run.Started += () => console.Expand();
            run.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ScriptRunController.State) && run.IsFaulted)
                    console.Expand();
            };
            return new DocumentParts(run, console, panel);
        });
}
