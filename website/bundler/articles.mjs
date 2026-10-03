import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { load } from 'js-yaml';
import { fromMarkdown } from 'mdast-util-from-markdown';
import { gfmFromMarkdown } from 'mdast-util-gfm';
import { gfm } from 'micromark-extension-gfm';
import { slugger } from './slug.mjs';

export function plain_text(node) {
  if (node.type === 'text' || node.type === 'inlineCode') return node.value;
  return (node.children ?? []).map(plain_text).join('');
}

function visit(node, fn) {
  fn(node);
  for (const child of node.children ?? []) visit(child, fn);
}

export async function read_markdown(file, context) {
  const source = (await readFile(file, 'utf8')).replace(/\r\n?/g, '\n');
  const tree = fromMarkdown(source, { extensions: [gfm()], mdastExtensions: [gfmFromMarkdown()] });
  const name = path.relative(context.repo_root, file).split(path.sep).join('/');

  visit(tree, node => {
    delete node.position;
    if (node.type === 'html') throw new Error(`${name}: raw html is not supported`);
    if (node.type === 'code' && node.meta == null) delete node.meta;
    if (node.type === 'link') node.url = link_target(node, file, context, name);
  });

  const slug = slugger();
  const headings = [];
  let title = null;
  tree.children = tree.children.filter(node => {
    if (node.type !== 'heading') return true;
    const text = plain_text(node);
    if (node.depth === 1 && title === null) {
      title = text;
      return false;
    }
    node.id = slug(text);
    if (node.depth <= 3) headings.push({ id: node.id, text, depth: node.depth });
    return true;
  });

  if (!title) throw new Error(`${name}: missing # title`);
  return { title, source: name, body: tree, headings };
}

function link_target(node, file, context, name) {
  const url = node.url;
  if (url.startsWith('xref:')) {
    const uid = decodeURIComponent(url.slice(5));
    const target = context.resolve(uid);
    if (!target?.url) throw new Error(`${name}: unresolved xref ${uid}`);
    const [only] = node.children;
    if (node.children.length === 1 && only.type === 'text' && only.value === url) {
      node.children = [{ type: 'text', value: target.name }];
    }
    return target.url;
  }
  if (/^[a-z][a-z0-9+.-]*:/i.test(url) || url.startsWith('#')) return url;

  const [relative, hash] = url.split('#');
  const target = path.relative(context.website_dir, path.resolve(path.dirname(file), relative)).split(path.sep).join('/');
  const suffix = hash ? `#${hash}` : '';
  if (target === 'api/index.md') return `api:${suffix}`;
  const article = /^docs\/(.+)\.md$/.exec(target);
  if (article) return `docs:${article[1]}${suffix}`;
  throw new Error(`${name}: link to ${url} does not point to an article`);
}

export async function build_guide(context) {
  const docs_dir = path.join(context.website_dir, 'docs');
  const toc = load(await readFile(path.join(docs_dir, 'toc.yml'), 'utf8'));
  const articles = {};

  async function walk(entries) {
    const nav = [];
    for (const entry of entries ?? []) {
      if (entry.href) {
        const slug = entry.href.replace(/\.md$/, '');
        if (articles[slug]) throw new Error(`docs/toc.yml lists ${entry.href} twice`);
        articles[slug] = await read_markdown(path.join(docs_dir, entry.href), context);
        nav.push({ title: entry.name, slug });
      } else {
        nav.push({ title: entry.name, items: await walk(entry.items) });
      }
    }
    return nav;
  }

  const nav = await walk(toc);
  return { nav, articles };
}
