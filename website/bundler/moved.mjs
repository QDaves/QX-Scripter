import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { load } from 'js-yaml';

export async function read_moved(file, pages, repo_root) {
  const source = path.relative(repo_root, file).split(path.sep).join('/');
  const moved = load(await readFile(file, 'utf8'));
  if (typeof moved !== 'object' || moved === null || Array.isArray(moved)) {
    throw new Error(`${source}: expected a map from old to new api pages`);
  }
  for (const [from, to] of Object.entries(moved)) {
    if (Object.hasOwn(pages, from)) throw new Error(`${source}: ${from} is still a page`);
    if (typeof to !== 'string' || !Object.hasOwn(pages, to)) throw new Error(`${source}: ${from} moves to ${to}, which is not a page`);
  }
  return moved;
}
