# API reference

This reference documents every public type and member of the assemblies scripts compile against.

Scripts run with the members of <xref:Qx.Scripting.ScriptGlobals> in scope, so `Room`, `Users`,
`Talk` and `OnIn` are written without a prefix. The other types are what those members return or
accept, and the host that compiles and runs scripts.

A script imports every namespace marked imported. A type from a namespace marked add using needs a
`using` directive or its full name, and the host namespaces make up the app around the scripts.

| Namespace | Contents | In scripts |
| --- | --- | --- |
| <xref:Qx.Scripting> | The script globals, the panel and keyboard APIs, queries, room scopes and the helper types of the globals. | imported |
| <xref:Qx.Game> | The live game state: the room, inventory, friends, catalog, marketplace, Wired and the other managers, their events, the game data and request errors. | imported |
| <xref:Qx.Model> | Avatars, furni, rooms, users, groups, forums, the marketplace, polls, quests and the other game models. | imported |
| <xref:Qx.Model.Figures> | Avatar figures, their parts and colors, and figure validation. | imported |
| <xref:Qx.Model.Wired> | Wired boxes, variables, chests and their messages. | imported |
| <xref:Qx.Model.Messages.Incoming> | Parsed messages the hotel sends. | imported |
| <xref:Qx.Model.Messages.Outgoing> | Messages the client sends. | imported |
| <xref:Qx.Game.Application> | The application layer behind `Application`, the MCP `application_*` tools and the command line: member ids, requests, results and events. | imported |
| <xref:Qx.Interception> | Packet interception: the interceptor, intercepted packets and the hotel session. | imported |
| <xref:Qx.Messages> | Packets, headers, packet readers and writers, and wire profiles. | imported |
| <xref:Qx.Protocol> | Message keys, the Flash message names in `Msg`, and the message registry and catalogs. | imported |
| <xref:Qx.Platform> | The operating system, keys and the raw keyboard reader. | imported |
| <xref:Qx> | Ids, lengths, message directions, the storage paths and the product version. | imported |
| <xref:Qx.Game.Protocol> | Message contracts, which bind a message key to its model and wire layout. | add using |
| <xref:Qx.Game.Snapshots> | The JSON read model the MCP read tools return. | add using |
| <xref:Qx.Scripting.Hosting> | Compiling and running scripts, panel layouts and the API catalog. | host |
| <xref:Qx.Interception.GEarth> | The G-Earth extension that connects QX Scripter to G-Earth. | host |
| <xref:Qx.Game.Rules> | Session rules such as anti-idle, chat muting and click actions. | host |
| <xref:Qx.Diagnostics> | The diagnostic log. | host |
