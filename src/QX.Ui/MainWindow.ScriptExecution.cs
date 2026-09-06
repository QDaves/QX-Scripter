using System.IO;
using Microsoft.CodeAnalysis;
using Qx.Hosting;
using Qx.Scripting;

namespace Qx.Ui;

public partial class MainWindow
{
    private async Task RunTabCoreAsync(ScriptTab tab, string? panel_button, bool panel_mode)
    {
        var run_source = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        tab.Cts = run_source;
        tab.BeginExecution();
        ScriptGlobals? configured_globals = null;
        PanelRun? live = null;
        Action<string, string>? panel_changed = null;
        ScriptRunState terminal_state = ScriptRunState.Faulted;
        int terminal_published = 0;
        string source = tab.CurrentCode;
        string file_name = tab.FilePath ?? ScriptName.Normalize(tab.Name) + ScriptName.Extension;
        var output_names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var button_names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var table_names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? default_output = null;
        KeyValuePair<string, string>[] panel_values = [];

        void AppendPanelOutput(string box, string text)
        {
            string output_name = ResolveOutput(box, output_names, default_output);
            if (output_name.Length == 0)
                return;

            Dispatch(() =>
            {
                if (!ReferenceEquals(tab.Cts, run_source))
                    return;

                bool truncated = tab.PanelState.AppendOutput(output_name, text);
                if (IsVisiblePanel(tab))
                {
                    if (truncated)
                        Panel.SetOutput(output_name, tab.PanelState.OutputValue(output_name));
                    else
                        Panel.Append(output_name, text);
                }
            });
        }

        void Emit(string message) => AppendOutput(tab, message);

        void ReportError(ScriptExecutionError error)
        {
            Emit(error.Format());
            Dispatch(() =>
            {
                if (ReferenceEquals(tab.Cts, run_source))
                    tab.AddError(error);
            });
        }

        void ReadPanelValues()
        {
            if (configured_globals is null || !IsVisiblePanel(tab))
                return;
            IReadOnlyDictionary<string, string> current = Panel.Values();
            tab.PanelState.SaveValues(current);
            configured_globals.Ui.Changed -= panel_changed;
            foreach ((string name, string value) in current)
                configured_globals.Ui.Set(name, value);
            configured_globals.Ui.Changed += panel_changed;
        }

        async Task ConfigureAsync(ScriptGlobals globals, CancellationToken cancellation_token)
        {
            configured_globals = globals;
            if (!panel_mode)
                return;

            await InvokeUiAsync(() =>
            {
                foreach ((string name, string value) in panel_values)
                    globals.Ui.Set(name, value);
                globals.Ui.SetClicked(panel_button);
                globals.Ui.Logged += AppendPanelOutput;
                globals.Ui.Downloaded += (file, content) => Dispatch(() => SaveDownload(tab, file, content));

                bool Live() => ReferenceEquals(tab.Cts, run_source) && IsVisiblePanel(tab);
                globals.Ui.Cleared += box => Dispatch(() =>
                {
                    if (table_names.Contains(box))
                    {
                        if (Live())
                            Panel.ClearTable(box);
                        return;
                    }

                    string name = ResolveOutput(box, output_names, default_output);
                    if (name.Length == 0)
                        return;
                    tab.PanelState.SetOutput(name, "");
                    if (Live())
                        Panel.ClearOutput(name);
                });
                panel_changed = (name, value) => Dispatch(() =>
                {
                    tab.PanelState.SetValue(name, value);
                    if (Live())
                        Panel.SetFieldValue(name, value);
                });
                globals.Ui.Changed += panel_changed;
                globals.Ui.ProgressChanged += (name, value) => Dispatch(() =>
                {
                    if (Live())
                        Panel.SetProgress(name, value);
                });
                globals.Ui.StatusChanged += (name, text) => Dispatch(() =>
                {
                    if (Live())
                        Panel.SetStatus(name, text);
                });
                globals.Ui.EnabledChanged += (name, enabled) => Dispatch(() =>
                {
                    if (Live())
                        Panel.SetEnabled(name, enabled);
                });
                globals.Ui.VisibilityChanged += (name, visible) => Dispatch(() =>
                {
                    if (Live())
                        Panel.SetVisible(name, visible);
                });
                globals.Ui.RowAdded += (table, cells) => Dispatch(() =>
                {
                    if (Live())
                        Panel.AddRow(table, cells);
                });
                globals.Ui.Toasted += (text, problem) => Dispatch(() =>
                {
                    if (Live())
                        Panel.Toast(text, problem);
                });
                globals.Ui.BusyChanged += (button, busy) => Dispatch(() =>
                {
                    if (Live())
                        Panel.SetButtonBusy(button, busy);
                });
                globals.Ui.ConfirmRequested += (title, message) => AskScriptAsync(tab, run_source, title, message);
                globals.Ui.PromptRequested += (title, initial) => PromptScriptAsync(tab, run_source, title, initial);
                return true;
            }, cancellation_token);
        }

        async Task ContinueAsync(ScriptGlobals globals, CancellationToken cancellation_token)
        {
            if (!panel_mode)
                return;

            await InvokeUiAsync(() =>
            {
                if (panel_button is { Length: > 0 } && IsVisiblePanel(tab))
                    Panel.SetButtonBusy(panel_button, false);
                if (!globals.Ui.HasClickHandlers)
                    return true;

                foreach (string handled in globals.Ui.HandledButtons)
                {
                    if (!button_names.Contains(handled))
                        Emit($"warning: Ui.OnClick(\"{handled}\", ...) has no //@ui:button {handled}");
                }

                live = new PanelRun
                {
                    Globals = globals,
                    Sync = ReadPanelValues,
                    Report = error => Dispatch(() =>
                    {
                        ScriptExecutionError issue = ScriptExecutionError.FromException(error, "handler", file_name);
                        tab.AddError(issue);
                        Emit(issue.Format());
                    })
                };
                _panelRuns[tab] = live;
                tab.PanelArmed = true;
                if (IsVisiblePanel(tab))
                    Panel.SetBusy(false);
                if (panel_button is { Length: > 0 })
                    FirePanelHandler(tab, live, panel_button);
                return true;
            }, cancellation_token);

            if (live is null)
                return;
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellation_token);
            }
            catch (OperationCanceledException) when (live.Finished)
            {
            }
        }

        async Task DrainAsync()
        {
            PanelRun? current = live;
            if (current is null)
                return;
            await InvokeUiAsync(() =>
            {
                if (ReferenceEquals(_panelRuns.GetValueOrDefault(tab), current))
                    _panelRuns.Remove(tab);
                return true;
            }, CancellationToken.None);
            await current.DrainAsync();
        }

        try
        {
            tab.Output.Clear();
            tab.OutputCollapsed = false;
            if (tab == Active)
            {
                ApplyOutputState(tab);
                RefreshOutput(tab);
            }

            if (panel_mode)
            {
                if (IsVisiblePanel(tab))
                    tab.PanelState.SaveValues(Panel.Values());
                UiSpec panel_spec = UiSpec.Parse(source);
                foreach (UiOutput output in panel_spec.Outputs)
                {
                    output_names.TryAdd(output.Name, output.Name);
                    default_output ??= output.Name;
                }
                foreach (UiButton declared in panel_spec.Buttons)
                    button_names.Add(declared.Name);
                foreach (UiTableNode declared in panel_spec.Tables)
                    table_names.Add(declared.Name);
                panel_values = tab.PanelState.Values.ToArray();
                if (IsVisiblePanel(tab))
                {
                    Panel.SetBusy(true);
                    if (panel_button is { Length: > 0 })
                        Panel.SetButtonBusy(panel_button, true);
                }
            }

            if (tab.FilePath is not null)
            {
                File.WriteAllText(tab.FilePath, source);
                tab.IsModified = false;
            }

            ScriptExecutionResult result = await _script_execution.RunAsync(new ScriptExecutionRequest
            {
                Code = source,
                SourceIdentity = tab.FilePath is null
                    ? tab.ExecutionIdentity
                    : Path.GetFullPath(tab.FilePath),
                FileName = file_name,
                OutputWritten = Emit,
                DiagnosticReported = diagnostic =>
                {
                    if (diagnostic.Severity == DiagnosticSeverity.Warning)
                    {
                        ScriptExecutionError warning = ScriptExecutionError.FromDiagnostic(diagnostic, file_name);
                        Emit($"Warning {warning.Format()}");
                    }
                },
                ErrorReported = ReportError,
                StateChanged = state => Dispatch(() =>
                {
                    if (!ReferenceEquals(tab.Cts, run_source))
                        return;
                    if (Volatile.Read(ref terminal_published) != 0 &&
                        state is ScriptRunState.Compiling or ScriptRunState.Running or ScriptRunState.Stopping)
                        return;
                    tab.SetRunState(state);
                }),
                ConfigureAsync = ConfigureAsync,
                ContinueAsync = ContinueAsync,
                DrainAsync = DrainAsync
            }, run_source.Token);

            terminal_state = result.State;
            Interlocked.Exchange(ref terminal_published, 1);
            if (terminal_state == ScriptRunState.Finished)
                Emit("[finished]");
            else if (terminal_state == ScriptRunState.Stopped)
                Emit("[stopped]");
            tab.SetRunState(terminal_state);
        }
        catch (Exception error)
        {
            ScriptExecutionError issue = ScriptExecutionError.FromException(error, "host", file_name);
            tab.AddError(issue);
            Emit(issue.Format());
            terminal_state = ScriptRunState.Faulted;
            Interlocked.Exchange(ref terminal_published, 1);
            tab.SetRunState(terminal_state);
        }
        finally
        {
            if (live is not null && ReferenceEquals(_panelRuns.GetValueOrDefault(tab), live))
                _panelRuns.Remove(tab);
            if (tab.IsSavedToDisk)
                _library.SetLastOutcome(tab.Name, terminal_state.ToString());
            run_source.Dispose();
            if (ReferenceEquals(tab.Cts, run_source))
            {
                tab.Cts = null;
                tab.PanelArmed = false;
            }
            if (panel_mode && IsVisiblePanel(tab))
            {
                Panel.SetBusy(false);
                if (panel_button is { Length: > 0 })
                    Panel.SetButtonBusy(panel_button, false);
            }
        }
    }
}
