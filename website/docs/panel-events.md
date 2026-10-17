# Panel events

A panel script initializes when its tab opens. It stays alive until the tab closes, and every
button press calls its handler instead of starting the script again. Game event subscriptions
remain active even in a panel with no UI handlers and while another tab is selected.

```csharp
//@ui:string message "Message" ="hello"
//@ui:button say "Say"

Ui.OnClick("say", () => Talk(Ui.String("message")));
```

## Handlers

`Ui.OnClick(name, handler)` takes an `Action` or a `Func<Task>`. Handlers run next to each other, so a
button that works for a minute does not stop the others from answering. The pressed button spins
and rejects additional presses until its handler completes. Other buttons remain usable.

Reads inside a handler return what the panel shows now, so a running loop sees edits made while it
runs.

`Ui.OnChange(name, handler)` runs when the user edits a control. The handler takes the new value as a
`string`, or takes nothing and reads the control itself. The `Func<string, Task>` overload supports
asynchronous work. Changes run in order, waiting for the preceding handler including its awaits.
A value the script writes with `Ui.Set` or `Ui.SetOptions` does not call them. Like a
click handler, a change handler keeps the script alive. An exception it throws is a script error and
stops the run.

```csharp
//@ui:select mode "Say it as" [Talk,Shout]
//@ui:status last "Mode"

Ui.OnChange("mode", mode => Ui.Status("last", $"saying it as {mode}"));
```

## Read

Each read returns the fallback when the control is missing or empty.

| Method | Returns |
| --- | --- |
| `Ui.String(name, fallback)`, `Ui.Text(...)`, `Ui.Select(...)` | `string` |
| `Ui.Int(name, fallback)` | `int` |
| `Ui.Number(name, fallback)` | `double` |
| `Ui.Bool(name, fallback)` | `bool`; only `true` and `1` count as yes |
| `Ui.File(name)` | The chosen path, or `null`. |
| `Ui.FileText(name)` | The chosen file's text, or an empty string. |

## Write

| Method | Description |
| --- | --- |
| `Ui.Log(box, text)` | Appends a line to an output box. |
| `Ui.Clear(name)` | Empties an output box or a table. |
| `Ui.Set(name, value)` | Changes what an input shows. |
| `Ui.Progress(name, value)`, `Ui.Progress(name, done, total)` | Sets a progress bar, 0 to 1. |
| `Ui.Progress(name, value, caption)` | Sets progress and explanatory text below the bar. |
| `Ui.Indeterminate(name, caption)` | Shows activity while the total is unknown. The next `Progress` returns to a measured bar. |
| `Ui.SetOptions(name, options, selected)` | Replaces dropdown choices, preserves a valid selection or selects the first, and updates reads immediately. |
| `Ui.Validation(name, message)` | Shows a field error. Pass `null` or an empty string to clear it. |
| `Ui.Status(name, text)` | Sets a status line. |
| `Ui.Enable(name, on)` | Enables or disables a field or button. |
| `Ui.Show(name, on)` | Shows or hides a control. A hidden control takes no space. |
| `Ui.Busy(button, busy)` | Shows a button as working and prevents additional presses. |
| `Ui.Toast(text, problem)` | Shows a short message that fades. |
| `Ui.Download(fileName, content)` | Offers a file to save. |
| `Ui.AddRow(table, cells)`, `Ui.SetRows(table, rows)` | Fills a table. |

The pressed button spins while its handler runs. `Ui.Busy` is for work that goes on after the handler
returns.

## Ask

```csharp
if (await Ui.Confirm("Shout?", "Every line goes to the whole room."))
    Shout("hello");

string? name = await Ui.Prompt("Name", "");
```

`Ui.Confirm` returns `false` and `Ui.Prompt` returns `null` when the dialog is dismissed and also when
nobody can answer: the run was stopped, the tab was closed, or there is no panel at all, which is
every command line and MCP run. Never let a `Confirm` guard a destructive step in a script that can
run without a panel.

## Example

A start button that loops while stop stays responsive:

```csharp
//@ui:title Broadcaster
//@ui:row
//@ui:string message "Message" ="hello" grow=1
//@ui:button start "Start" style=primary
//@ui:button stop "Stop" style=danger
//@ui:endrow
//@ui:int rounds "Rounds" =10 min=1 max=200
//@ui:progress work "Progress"

bool sending = false;
Ui.Enable("stop", false);

Ui.OnClick("stop", () => sending = false);

Ui.OnClick("start", async () =>
{
    sending = true;
    Ui.Enable("start", false);
    Ui.Enable("stop", true);
    int rounds = Ui.Int("rounds", 10);
    try
    {
        for (int n = 1; sending && n <= rounds; n++)
        {
            Talk(Ui.String("message"));
            Ui.Progress("work", n, rounds);
            await Delay(500);
        }
    }
    finally
    {
        sending = false;
        Ui.Enable("start", true);
        Ui.Enable("stop", false);
    }
});
```

## Running and stopping

Opening a UI tab starts its initializer once. The toolbar shows **UI mode**, with actions and their
busy states controlled by panel buttons. F5 does not start or stop an execution while panel view is
shown. Switching to code view leaves the existing execution alive; normal code-view stop and the
global stop-all command remain available. Returning to panel view applies source changes.

A stop button declared by the script does what its handler specifies. Use the script's `Ct` token
for cancellable background work so closing the tab stops it promptly. Non-UI scripts retain normal
manual Run/Stop behavior. Command-line and MCP executions still require an explicit run request;
`//@ui:required` refuses those executions because they have no interactive panel.

UI initialization does not wait for a hotel connection. Register connection and game events during
initialization, and place operations that require hotel data in the relevant event or button handler.

## Scripts without handlers

A script with no UI handlers also stays alive, keeping its game-event subscriptions. Legacy forms
using `Ui.Clicked(name)` are supported: pressing a declared button stops the previous execution and
initializes a replacement with that button recorded. Prefer `Ui.OnClick` for new panels to avoid
reinitializing state on each action.
