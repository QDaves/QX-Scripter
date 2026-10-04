import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseArgs } from 'node:util';
import { gzipSync } from 'node:zlib';
import { build_api } from './api.mjs';
import { build_guide, read_markdown } from './articles.mjs';
import { read_moved } from './moved.mjs';
import { arrange_namespaces, check_import_list, read_imports } from './namespaces.mjs';

const format = 1;

const website_dir = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const repo_root = path.dirname(website_dir);

const { values: options } = parseArgs({
  options: {
    out: { type: 'string', default: path.join(website_dir, 'out') },
  },
});

function git(...args) {
  return execFileSync('git', args, { cwd: repo_root, encoding: 'utf8' }).trim();
}

async function build_version() {
  const props = await readFile(path.join(repo_root, 'Directory.Build.props'), 'utf8');
  const series = /<QxVersionSeries>([^<]+)<\/QxVersionSeries>/.exec(props)[1];
  const start = git('log', '-1', '--first-parent', '--format=%H', `-S<QxVersionSeries>${series}</QxVersionSeries>`, '--', 'Directory.Build.props');
  const builds = start ? Number(git('rev-list', '--first-parent', '--count', `${start}..HEAD`)) + 1 : 1;
  return `${series}.${builds}`;
}

function sha256(data) {
  return createHash('sha256').update(data).digest('hex');
}

function check_links(bundle) {
  const problems = [];
  function visit(node, where) {
    if (node.type === 'link') {
      const [scheme, rest = ''] = node.url.split(/:(.*)/s);
      const [target, anchor] = rest.split('#');
      if (scheme === 'docs') {
        const article = bundle.guide.articles[target];
        if (!article) problems.push(`${where}: missing article ${target}`);
        else if (anchor && !article.headings.some(heading => heading.id === anchor)) problems.push(`${where}: missing heading ${node.url}`);
      } else if (scheme === 'api' && target && !bundle.api.pages[target]) {
        problems.push(`${where}: missing api page ${target}`);
      }
    }
    for (const child of node.children ?? []) visit(child, where);
  }
  for (const [slug, article] of Object.entries(bundle.guide.articles)) visit(article.body, `docs/${slug}.md`);
  visit(bundle.api.index.body, 'api/index.md');
  if (problems.length) throw new Error(problems.join('\n'));
}

const repository = JSON.parse(await readFile(path.join(website_dir, 'package.json'), 'utf8')).repository;
const api = await build_api(path.join(website_dir, 'api'), repo_root);
const context = { repo_root, website_dir, resolve: api.resolve };
const guide = await build_guide(context);
const index = await read_markdown(path.join(website_dir, 'api', 'index.md'), context);
const imports = await read_imports(repo_root);
check_import_list(guide, imports);
const namespaces = arrange_namespaces(api, index, imports);
const moved = await read_moved(path.join(website_dir, 'moved.yml'), api.pages, repo_root);

const content = {
  guide,
  api: {
    index,
    namespaces,
    pages: api.pages,
    moved,
  },
};

const bundle = {
  format,
  version: await build_version(),
  commit: git('rev-parse', 'HEAD'),
  repository,
  ...content,
};
check_links(bundle);

const data = gzipSync(JSON.stringify(bundle), { level: 9 });
const manifest = {
  format,
  version: bundle.version,
  commit: bundle.commit,
  hash: sha256(JSON.stringify(content)),
  file: 'docs.json.gz',
  sha256: sha256(data),
  size: data.length,
};

await mkdir(options.out, { recursive: true });
await writeFile(path.join(options.out, manifest.file), data);
await writeFile(path.join(options.out, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n');

console.log(
  `docs ${manifest.version}: ${Object.keys(guide.articles).length} articles, ` +
    `${Object.keys(api.pages).length} api pages, ${(data.length / 1024 / 1024).toFixed(2)} MB`,
);
