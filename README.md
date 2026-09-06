# QX Scripter

QX Scripter is a C# scripting extension for G-Earth for Flash and Unity.

### Currently in **alpha**. Expect bugs and changes to the script API. Bug reports and pull requests are welcome.

<img width="827" height="664" alt="9YYg7nW" src="https://github.com/user-attachments/assets/0d772294-0b4d-41e1-8a1a-970a286e7991" />


## Community scripts

Find and share scripts at [qxscripter.xyz](https://qxscripter.xyz/)

## MCP

QX exposes game data, scripting and desktop editor access through MCP. Copy the connection URL from Settings and add it to a client that supports Streamable HTTP. The default endpoint is `http://127.0.0.1:9390/mcp`.

Once connected, this MCP request checks a script without running it:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "compile_check",
    "arguments": { "code": "Log(RoomId);" }
  }
}
```

Use `get_connection` to check the session, `list_api` to find script methods and `run_code` to run a script.

## Building from source

Requires the .NET SDK version in [global.json](global.json).

```powershell
git clone https://github.com/QDaves/QX-Scripter.git
cd QX-Scripter
dotnet restore QX.slnx --locked-mode
dotnet build QX.slnx -c Release --no-restore
```

Run the desktop application:

```powershell
dotnet run --project src/QX.Ui -c Release --no-build
```

Or run the CLI:

```powershell
dotnet run --project src/QX.App -c Release --no-build -- -p 9092 -q
```

## CLI

The CLI runs scripts and exposes game operations without the desktop window. It also provides MCP, but has no editor or visual script panels.

Download the CLI archive and install the [.NET 10 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). The Desktop Runtime also works.

```powershell
.\QX.exe -p 9092 -q --script .\my-script.csx
```

Use `QX.exe app help` to list the available application commands.

---

Created by [QDave](https://github.com/QDaves). Thanks to [b7](https://github.com/b7c) and his work on xabbo.
