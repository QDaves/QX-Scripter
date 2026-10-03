# Panel events

A panel script registers handlers and returns. The script stays alive, and every button press calls
its handler instead of starting the script again.

```csharp
//@ui:string message "Message" ="hello"
//@ui:button say "Say"

Ui.OnClick("say", () => Talk(Ui.String("message")));
```

## Handlers

`Ui.OnClick(name, handler)` takes an `Action` or a `Func<Task>`. Handlers run next to each other, so a
button that works for a minute does not stop the others from answering. A handler that must not run
twice disables its own button while it works.

Reads inside a handler return what the panel shows now, so a running loop sees edits made while it
runs.

`Ui.OnChange(name, handler)` runs when the user edits a control. The handler takes the new value as a
`string`, or takes nothing and reads the control itself. Change handlers start one at a time, in the
order the edits were made, each once the one before has returned, so an async handler lets the next
one start at its first `await`. A value the script writes with `Ui.Set` does not call them. Like a
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
| `Ui.Status(name, text)` | Sets a status line. |
| `Ui.Enable(name, on)` | Enables or disables a button. |
| `Ui.Show(name, on)` | Shows or hides a control. A hidden control takes no space. |
| `Ui.Busy(button, busy)` | Shows a button as working without disabling it. |
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

**Run** starts a script in the view that is shown. In panel view the run stays alive on its handlers
after the script's last statement. In code view it ends there and stops everything it started, so a
panel script that must keep running declares `//@ui:required`.

The toolbar **Stop** always ends the run. A stop button the script declares only does what its
handler does.

## Scripts without handlers

A script that registers no handlers runs from the top on every press and ends. `Ui.Clicked(name)`
says whether a button started it and `Ui.ClickedButton` names the button. This suits a form: fill
it in, press once, read the result.

Outside panel view every read returns its fallback, `Ui.Clicked` is `false`, handlers are never
called and writes do nothing, so a panel script also runs from the editor.
