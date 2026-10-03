# Panels

A script declares its own user interface with `//@ui:` directives. The desktop app shows it in the
tab's panel view.

```csharp
//@ui:title Greeter
//@ui:desc Says hello to everyone who enters.
//@ui:string greeting "Greeting" ="hello"
//@ui:button start "Start"
```

Every directive is named first and labeled second. The name is what the script uses, the quoted
text is what the panel shows. Without a label the name is turned into one, so `max_speed` becomes
"Max speed".

## Page

| Directive | Description |
| --- | --- |
| `//@ui:title Text` | The panel title. |
| `//@ui:desc Text` | A line of text under the title. |
| `//@ui:required` | The script only works with its panel. See below. |
| `//@ui:console [collapsed] [height=N]` | Shows the script's own output under the panel. |
| `//@ui:layout [center] [width=N \| full]` | Places the content. Left-aligned at 720 pixels by default. |

## Inputs

| Directive | Reads as |
| --- | --- |
| `//@ui:string name "Label" ="default"` | `string` |
| `//@ui:text name "Label"` | `string`, multi-line |
| `//@ui:int name "Label" =5 min=0 max=10` | `int` |
| `//@ui:number name "Label" =1.5` | `double` |
| `//@ui:slider name "Label" =50 min=0 max=100` | `double` |
| `//@ui:bool name "Label" =true` | `bool` |
| `//@ui:select name "Label" [A,B,C] =A` | `string`, the first option without a default |
| `//@ui:file name "Label"` | a file path |
| `//@ui:color name "Label" ="#8EA2FF"` | `string` |

Inputs also take `help="..."`, a line under the control, and `placeholder="..."`, hint text inside
it. `min` and `max` are a hint on `int` and `number` and a real range on `slider`.

## Outputs

| Directive | Description |
| --- | --- |
| `//@ui:label "Text"` | Static text. |
| `//@ui:button name "Label" [style=primary\|normal\|quiet\|danger]` | A button. The first one is filled. |
| `//@ui:output name "Label" [height=200] [wrap] [mono=false] [toolbar=false]` | A text box the script writes to. |
| `//@ui:progress name "Label"` | A progress bar. |
| `//@ui:status name "Label" ="text"` | A status line. |
| `//@ui:table name "Label" [Col,Col] [height=220] [selectable=false] [toolbar=false]` | A table. |

## Layout

| Directive | Description |
| --- | --- |
| `//@ui:row [gap=12] [align=start\|center\|end\|stretch]` ... `//@ui:endrow` | Places controls side by side. |
| `//@ui:group "Title" [collapsed=true]` ... `//@ui:endgroup` | A titled box that folds away. |
| `//@ui:section Heading` | A heading. |
| `//@ui:separator` | A line. |
| `//@ui:spacer [height=12]` | Empty space. |

Every control takes `width=` and `grow=` for rows and `tooltip="..."`. `grow` shares the leftover
width of a row, `width` fixes it. A row or group left open is closed by the end of the file.

```csharp
//@ui:row
//@ui:string message "Message" grow=1 placeholder="what to say"
//@ui:button send "Send" style=primary
//@ui:endrow
```

Buttons appear where they are declared. Put one in a row next to the input it acts on.

## Flags

A flag can be written alone, so `wrap` and `wrap=true` mean the same. An attribute the panel does not
know is kept and ignored.

A directive the panel does not know is ignored and reported as warning `QX1004`, and a `Ui` call on
a name the panel does not declare is reported as warning `QX1005`. A name the script stores with
`Ui.Set` counts as declared.

## Required panels

A script with `//@ui:required` opens in panel view, and **Run** starts it there. Runs without a panel,
such as the command line or the MCP tools `run_script` and `run_code`, are refused with an error
instead of running with default values.

## Tables

The columns are the bracket list. `Ui.AddRow` appends a row, `Ui.SetRows` replaces every row at once
and keeps the scroll position and the selection, and `Ui.Clear` empties the table.

```csharp
Ui.SetRows("users", Users.Select(user => new object[] { user.Name, user.X, user.Y }));
```

`Ui.String(table)` returns the selected row as its cells joined with tabs:

```csharp
string[] cells = Ui.String("users").Split('\t');
```

A table and an output box are separate controls even with the same name. Name them apart.

See [Panel events](panel-events.md) for the `Ui` API.
