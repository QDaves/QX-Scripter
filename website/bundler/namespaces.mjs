import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { plain_text } from './articles.mjs';

const engine_file = 'src/QX.Scripting/Hosting/ScriptEngine.cs';
const import_article = 'scripts';
const columns = ['Namespace', 'Contents', 'In scripts'];
const script_uses = new Map([
  ['imported', 'imported'],
  ['add using', 'using'],
  ['host', 'host'],
]);

export async function read_imports(repo_root) {
  const source = await readFile(path.join(repo_root, engine_file), 'utf8');
  const list = /\bImports\s*\{\s*get;\s*\}\s*=\s*\[([^\]]*)\]/.exec(source);
  if (!list) throw new Error(`${engine_file}: no Imports list`);
  return [...list[1].matchAll(/"([^"]+)"/g)].map(match => match[1]);
}

function differences(expected, actual) {
  return [
    ...expected.filter(name => !actual.includes(name)).map(name => `missing ${name}`),
    ...actual.filter(name => !expected.includes(name)).map(name => `extra ${name}`),
  ];
}

export function check_import_list(guide, imports) {
  const article = guide.articles[import_article];
  if (!article) throw new Error(`docs/toc.yml does not list ${import_article}.md, which lists the imported namespaces`);
  const blocks = article.body.children;
  const lead = blocks.findIndex(node => node.type === 'paragraph' && plain_text(node).trimEnd().endsWith('namespaces are imported:'));
  const list = lead < 0 ? undefined : blocks[lead + 1];
  if (list?.type !== 'paragraph') throw new Error(`${article.source}: no list of the imported namespaces`);
  const listed = list.children.filter(node => node.type === 'inlineCode').map(node => node.value);
  const problems = differences(imports, listed);
  if (problems.length) throw new Error(`${article.source} does not match ${engine_file}: ${problems.join(', ')}`);
}

function namespace_table(index) {
  const table = index.body.children.find(
    node => node.type === 'table' && node.children[0].children.map(plain_text).join('|') === columns.join('|'),
  );
  if (!table) throw new Error(`${index.source}: no table with the columns ${columns.join(', ')}`);
  return table.children.slice(1);
}

export function arrange_namespaces(api, index, imports) {
  const known = new Map(api.namespaces.map(entry => [`api:${entry.path}`, entry]));
  const arranged = new Map();
  for (const row of namespace_table(index)) {
    const [name, contents, use] = row.children;
    const [link] = name.children;
    const entry = name.children.length === 1 && link.type === 'link' ? known.get(link.url) : undefined;
    if (!entry) throw new Error(`${index.source}: ${plain_text(name)} is not a namespace of the reference`);
    if (arranged.has(entry.name)) throw new Error(`${index.source}: ${entry.name} has more than one row`);
    if (!plain_text(contents).trim()) throw new Error(`${index.source}: ${entry.name} has no contents`);
    const value = plain_text(use).trim();
    const scripts = script_uses.get(value);
    if (!scripts) throw new Error(`${index.source}: ${entry.name} is "${value}" in scripts, not ${[...script_uses.keys()].join(', ')}`);
    api.pages[entry.path].summary = { type: 'root', children: [{ type: 'paragraph', children: contents.children }] };
    arranged.set(entry.name, { ...entry, scripts });
  }

  const missing = api.namespaces.filter(entry => !arranged.has(entry.name)).map(entry => entry.name);
  if (missing.length) throw new Error(`${index.source}: no row for ${missing.join(', ')}`);
  const imported = [...arranged.values()].filter(entry => entry.scripts === 'imported').map(entry => entry.name);
  const problems = differences(imports.filter(name => name.split('.')[0] === 'Qx'), imported);
  if (problems.length) throw new Error(`${index.source}: the imported rows do not match ${engine_file}: ${problems.join(', ')}`);
  return [...arranged.values()];
}
