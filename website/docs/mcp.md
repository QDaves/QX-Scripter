# MCP

QX Scripter runs an MCP server, so an AI client can read the game state, write and run scripts and
work in the editor.

## Connect

The server listens on `http://127.0.0.1:9390/mcp` with the Streamable HTTP transport. Copy the
connection URL from the settings of the desktop app; it contains the access token.

## Several instances

One port serves one QX window. A second window, for example for a second account, runs without MCP
and logs that the port is taken. To give it its own server, start it with the environment variable
`QX_MCP_PORT` set to a free port such as `9391`.

On Windows the server uses http.sys, so tools like `netstat` report every MCP port as held by System
(pid 4) instead of the QX process.

## Permissions

The server is configured in `mcp.json` in the QX Scripter configuration folder, next to the
`scripts` folder.

| Setting | Allows |
| --- | --- |
| `allowExecute` | Compiling and running code. |
| `allowFileWrite` | Creating, changing, renaming and deleting scripts. |
| `allowEditor` | Reading and changing the editor tabs. |
| `requireAuth` | When `false`, the token check is skipped. |
| `toolFilter` | The tools `tools/list` offers, instead of the default set. |

The switches protect the QX process, the script files and the editor; game reads and actions,
including the `application_*` tools, `send_to_server`, `send_to_client` and `remove_rights`, are never
gated, and `toolFilter` only changes what `tools/list` offers.

## Tools

`tools/list` offers the everyday tools. Several hundred more game operations are named
`application_*`. `list_mcp_tools` searches all of them, `describe_mcp_tool` shows one schema and
`call_mcp_tool` or `read_mcp_tool` calls one.

| Area | Tools |
| --- | --- |
| Learn the API | `get_scripting_guide`, `list_api`, `search_types`, `get_type`, `search_members`, `list_application_members`, `describe_application_member` |
| Scripts | `list_scripts`, `read_script`, `outline_script`, `find_in_script`, `get_script_part` |
| Change scripts | `patch_script`, `replace_script_lines`, `save_script`, `rename_script`, `delete_script` |
| Run code | `compile_check`, `run_code`, `run_script`, `get_run`, `stop_run` |
| Editor | `list_tabs`, `open_tab`, `create_tab`, `select_tab`, `close_tab`, `run_tab`, `stop_tab`, `get_tab_output`, `get_tab_status`, `get_tab_errors` |
| Game state | `get_connection`, `get_room`, `get_avatars`, `get_furni`, `get_inventory`, `get_friends` and more |
| Packets | `get_protocol_messages`, `send_to_server`, `send_to_client` |

`list_api`, `search_types`, `search_members` and `list_application_members` return one page at a time,
with the total and the offset of the next page. `describe_application_member` also shows the C# call
that reaches the member from a script.

`get_scripting_guide` returns an overview and the list of topics. With a topic it returns that page of
this documentation, so the whole guide is available to the client.

## Scripts

A script that is open in the editor is read and changed live in its tab. A tab without unsaved changes
is saved at once, so there is no need to save, close and reopen it. `patch_script` replaces exact text
and `replace_script_lines` replaces lines by number, so small changes do not resend the file.

## Test code

`run_code` runs code without saving it or opening a tab. With `background=true` it returns a run id at
once; `get_run` reads the state and the new output lines and `stop_run` ends the run.

```json
{"name": "run_code", "arguments": {"code": "#load \"Maze Engine.csx\"\nawait WalkAndWait(4, 7);", "background": true}}
{"name": "get_run", "arguments": {"id": 1, "since": 0}}
{"name": "stop_run", "arguments": {"id": 1}}
```

A background run has no time limit unless `timeout_ms` is given. The desktop app counts it as a
running script, and **Stop every running script** or the panic key ends it.
