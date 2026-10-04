# Website content

The guides and the scripting API reference shown on [qxscripter.xyz](https://qxscripter.xyz).

- `docs/` holds the guides. `docs/toc.yml` sets their order. QX Scripter embeds the same files for its MCP server,
  whose whole-guide read leaves out the pages under the release notes group.
- `api/index.md` is the front page of the API reference. Its namespace table gives every namespace page its summary,
  sets the order of the namespaces and says which ones scripts import.
- The API reference itself comes from the XML documentation comments in `src`.
- `moved.yml` maps API pages that were renamed or whose namespace was retired to their new pages, so the site can
  redirect old links.

## Publishing

The Docs workflow runs on every push to `main` that touches `src` or `website`. It generates the API metadata with
DocFX, turns it and the guides into one bundle and publishes `manifest.json` and `docs.json.gz` to the `docs` branch
when the content changed. The site checks that branch on start and every 10 hours.

The bundle build fails on broken links, so a guide that links to a missing article, heading or type does not ship. It
also fails when the namespace table misses a namespace, or when its imported rows or the import list in
`docs/scripts.md` differ from the imports in `ScriptEngine.cs`, or when `moved.yml` maps a page that still exists or
points to one that does not.

## Local build

```bash
dotnet tool install --global docfx --version 2.81.0
docfx metadata website/docfx.json
cd website
npm ci
npm run bundle
```

The bundle lands in `website/out`.
