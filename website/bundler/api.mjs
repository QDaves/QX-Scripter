import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import { load } from 'js-yaml';
import { markup_to_mdast } from './markup.mjs';
import { slugger } from './slug.mjs';

const type_kinds = {
  Class: 'class',
  Struct: 'struct',
  Interface: 'interface',
  Enum: 'enum',
  Delegate: 'delegate',
};

const member_kinds = {
  Constructor: 'constructor',
  Field: 'field',
  Property: 'property',
  Method: 'method',
  Event: 'event',
  Operator: 'operator',
};

const member_sections = [
  ['constructor', 'Constructors'],
  ['field', 'Fields'],
  ['property', 'Properties'],
  ['method', 'Methods'],
  ['event', 'Events'],
  ['operator', 'Operators'],
];

const type_groups = [
  ['class', 'Classes'],
  ['struct', 'Structs'],
  ['interface', 'Interfaces'],
  ['enum', 'Enums'],
  ['delegate', 'Delegates'],
];

const type_sections = ['remarks', 'examples', 'fields', 'see-also'];

const topic_member_limit = 150;

const topic_titles = new Map([
  ['Raw', 'Identity, wallet and runtime'],
  ['Helpers', 'Lookups and helpers'],
  ['Messages', 'Receiving packets'],
]);

const tuple_element = /^System\.ValueTuple\{.*\}\.[^.]+$/;

const by_name = (a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0);

function page_path(uid) {
  const value = uid
    .replace(/\*$/, '')
    .replace(/#c?ctor/, match => '-' + match.slice(1))
    .replace(/[`#]/g, '-');
  if (!/^[A-Za-z0-9._-]+$/.test(value)) throw new Error(`cannot map uid to a page path: ${uid}`);
  return value;
}

function topic_suffix(file, type) {
  const name = type.name.replace(/<.*$/, '');
  const base = path.basename(file, '.cs');
  return base === name ? '' : base.startsWith(`${name}.`) ? base.slice(name.length + 1) : base;
}

function topic_title(suffix) {
  if (!suffix) return 'General';
  const words = suffix.replace(/(?<=[a-z0-9])(?=[A-Z])/g, ' ').toLowerCase();
  return topic_titles.get(suffix) ?? words[0].toUpperCase() + words.slice(1);
}

function is_obsolete(item) {
  return item.attributes?.some(attribute => attribute.type === 'System.ObsoleteAttribute') ?? false;
}

function topics(type, rows) {
  const files = new Map();
  for (const row of rows) {
    const file = row.member.source?.path ?? type.source?.path ?? '';
    (files.get(file) ?? files.set(file, []).get(file)).push(row);
  }
  if (rows.length <= topic_member_limit || files.size < 2) return undefined;

  const rank = topic => (!topic.suffix ? 0 : topic.obsolete ? 2 : 1);
  const slug = slugger([...type_sections, ...member_sections.map(([kind]) => `${kind}s`), ...rows.map(row => row.anchor)]);
  return [...files]
    .map(([file, items]) => {
      const suffix = topic_suffix(file, type);
      return { suffix, title: topic_title(suffix), obsolete: items.every(row => is_obsolete(row.member)), items };
    })
    .sort((a, b) => rank(a) - rank(b) || (a.title < b.title ? -1 : a.title > b.title ? 1 : 0))
    .map(topic => ({ id: slug(topic.title), title: topic.title, anchors: topic.items.map(row => row.anchor) }));
}

function external_url(uid) {
  if (!/^(System|Microsoft)\./.test(uid)) return null;
  const base = uid.replace(/\(.*$/, '').replace(/\*$/, '').replace(/`+/g, '-').replace(/[{}]/g, '').toLowerCase();
  return `https://learn.microsoft.com/dotnet/api/${base}`;
}

async function read_items(api_dir) {
  const files = (await readdir(api_dir)).filter(file => file.endsWith('.yml') && file !== 'toc.yml');
  const items = [];
  const references = new Map();
  for (const file of files) {
    const doc = load(await readFile(path.join(api_dir, file), 'utf8'));
    for (const item of doc?.items ?? []) items.push(item);
    for (const ref of doc?.references ?? []) {
      const known = references.get(ref.uid);
      if (!known || (ref['spec.csharp'] && !known['spec.csharp'])) references.set(ref.uid, ref);
    }
  }
  return { items, references };
}

export async function build_api(api_dir, repo_root) {
  const { items, references } = await read_items(api_dir);
  const website_dir = path.dirname(api_dir);

  const namespaces = items.filter(item => item.type === 'Namespace');
  const types = items.filter(item => type_kinds[item.type]);
  const type_by_uid = new Map(types.map(item => [item.uid, item]));
  const members = items.filter(item => member_kinds[item.type]);
  const member_by_uid = new Map(members.map(item => [item.uid, item]));

  const targets = new Map();
  const pages = {};

  for (const ns of namespaces) targets.set(ns.uid, { url: `api:${page_path(ns.uid)}`, name: ns.name, name_with_type: ns.name });
  for (const type of types) targets.set(type.uid, { url: `api:${page_path(type.uid)}`, name: type.name, name_with_type: type.nameWithType });

  const groups = new Map();
  const enum_anchors = new Map();
  for (const member of members) {
    const owner = type_by_uid.get(member.parent);
    if (!owner) continue;
    if (owner.type === 'Enum') {
      const slug = enum_anchors.get(owner.uid) ?? enum_anchors.set(owner.uid, slugger(type_sections)).get(owner.uid);
      const anchor = slug(member.name);
      targets.set(member.uid, {
        url: `api:${page_path(owner.uid)}#${anchor}`,
        name: member.name,
        name_with_type: member.nameWithType,
        anchor,
      });
      continue;
    }
    const key = member.overload ?? member.uid;
    const group = groups.get(key) ?? groups.set(key, { key, owner, items: [] }).get(key);
    group.items.push(member);
  }

  const paths = new Set([...targets.values()].map(target => target.url));
  for (const group of groups.values()) {
    group.path = page_path(group.key);
    if (paths.has(`api:${group.path}`)) throw new Error(`page path collision: ${group.path}`);
    paths.add(`api:${group.path}`);
    group.items.sort(by_name);
    const slug = slugger();
    for (const member of group.items) {
      member.anchor = group.items.length > 1 ? slug(member.name) : null;
      targets.set(member.uid, {
        url: `api:${group.path}${member.anchor ? '#' + member.anchor : ''}`,
        name: member.name,
        name_with_type: member.nameWithType,
      });
    }
    const overload = references.get(group.key);
    group.name = overload?.name ?? group.items[0].name;
    targets.set(group.key, { url: `api:${group.path}`, name: group.name, name_with_type: overload?.nameWithType ?? group.name });
  }

  function resolve(uid) {
    const internal = targets.get(uid);
    if (internal) return internal;
    const ref = references.get(uid);
    const definition = ref?.definition && targets.get(ref.definition);
    if (definition) return { url: definition.url, name: ref.name, name_with_type: ref.nameWithType ?? ref.name };
    const url = ref?.href?.startsWith('http') ? ref.href : external_url(uid);
    if (!ref && !url) return null;
    return { url, name: ref?.name ?? uid.split('.').pop(), name_with_type: ref?.nameWithType ?? uid };
  }

  function text_of(item) {
    return markup => {
      try {
        return markup_to_mdast(markup, resolve);
      } catch (error) {
        throw new Error(`${item.uid}: ${error.message}`);
      }
    };
  }

  function link(uid, label) {
    const target = resolve(uid);
    const part = { text: label ?? target?.name ?? uid };
    if (target?.url) part.url = target.url;
    return part;
  }

  function type_ref(uid) {
    if (!uid) return null;
    const spec = references.get(uid)?.['spec.csharp'];
    if (!spec) return [link(uid)];
    return spec.map(part => {
      if (!part.uid || tuple_element.test(part.uid)) return { text: part.name };
      const target = targets.get(part.uid);
      if (target) return { text: part.name, url: target.url };
      const url = part.href?.startsWith('http') ? part.href : external_url(part.uid);
      return url ? { text: part.name, url } : { text: part.name };
    });
  }

  function source(item) {
    const location = item.source;
    if (!location?.path || typeof location.startLine !== 'number') return undefined;
    const file = path.relative(repo_root, path.resolve(website_dir, location.path)).split(path.sep).join('/');
    return { path: file, line: location.startLine + 1 };
  }

  function syntax(item) {
    const text = text_of(item);
    const block = item.syntax ?? {};
    return compact({
      declaration: block.content,
      type_params: block.typeParameters?.map(param => compact({ name: param.id, summary: text(param.description) })),
      params: block.parameters?.map(param =>
        compact({ name: param.id, type: type_ref(param.type), summary: text(param.description) }),
      ),
      returns: block.return ? compact({ type: type_ref(block.return.type), summary: text(block.return.description) }) : undefined,
    });
  }

  function details(item) {
    const text = text_of(item);
    return compact({
      summary: text(item.summary),
      remarks: text(item.remarks),
      examples: item.example?.map(text).filter(Boolean),
      exceptions: item.exceptions?.map(entry => compact({ type: type_ref(entry.type), summary: text(entry.description) })),
      see_also: item.seealso?.map(entry =>
        entry.linkType === 'HRef'
          ? { text: entry.altText || entry.linkId, url: entry.linkId }
          : link(entry.linkId, entry.altText || undefined),
      ),
      source: source(item),
    });
  }

  for (const type of types) {
    const kind = type_kinds[type.type];
    const children = (type.children ?? []).map(uid => member_by_uid.get(uid)).filter(Boolean);
    const page = compact({
      page: 'type',
      kind,
      uid: type.uid,
      name: type.name,
      full_name: type.fullName,
      namespace: type.namespace,
      assembly: type.assemblies?.[0],
      ...details(type),
      ...syntax(type),
      inheritance: type.inheritance?.map(uid => type_ref(uid)),
      implements: type.implements?.map(uid => type_ref(uid)),
      derived: type.derivedClasses?.map(uid => link(uid)),
      inherited: type.inheritedMembers?.map(uid => link(uid, resolve(uid)?.name_with_type)),
      extensions: type.extensionMethods?.map(uid => link(uid, resolve(uid)?.name_with_type)),
    });

    if (kind === 'enum') {
      page.fields = children.map(field =>
        compact({
          anchor: targets.get(field.uid).anchor,
          name: field.name,
          value: field.syntax?.content?.split('=').slice(1).join('=').trim() || undefined,
          summary: text_of(field)(field.summary),
          remarks: text_of(field)(field.remarks),
        }),
      );
    } else {
      const row_anchor = slugger();
      const rows = member_sections.flatMap(([member_kind]) =>
        children
          .filter(member => member_kinds[member.type] === member_kind)
          .map(member => ({ member, kind: member_kind, anchor: row_anchor(`${member_kind} ${member.name}`) })),
      );
      page.sections = member_sections
        .map(([member_kind, title]) => ({
          title,
          kind: member_kind,
          items: rows
            .filter(row => row.kind === member_kind)
            .map(({ member, anchor }) =>
              compact({
                anchor,
                name: member.name,
                url: targets.get(member.uid).url,
                summary: text_of(member)(member.summary),
              }),
            ),
        }))
        .filter(section => section.items.length);
      page.topics = topics(type, rows);
    }
    pages[page_path(type.uid)] = page;
  }

  for (const group of groups.values()) {
    const first = group.items[0];
    pages[group.path] = {
      page: 'member',
      kind: member_kinds[first.type],
      name: group.name,
      type: page_path(group.owner.uid),
      type_name: group.owner.name,
      type_kind: type_kinds[group.owner.type],
      namespace: group.owner.namespace,
      assembly: first.assemblies?.[0],
      overloads: group.items.map(member =>
        compact({
          anchor: member.anchor ?? undefined,
          name: member.name,
          ...details(member),
          ...syntax(member),
          overrides: member.overridden ? link(member.overridden, resolve(member.overridden)?.name_with_type) : undefined,
          implements: member.implements?.map(uid => link(uid, resolve(uid)?.name_with_type)),
        }),
      ),
    };
  }

  const namespace_list = namespaces
    .map(ns => {
      const ns_types = (ns.children ?? []).map(uid => type_by_uid.get(uid)).filter(Boolean);
      const entries = ns_types
        .map(type => compact({ name: type.name, kind: type_kinds[type.type], path: page_path(type.uid), summary: text_of(type)(type.summary) }))
        .sort(by_name);
      pages[page_path(ns.uid)] = compact({
        page: 'namespace',
        name: ns.name,
        groups: type_groups
          .map(([kind, title]) => ({ kind, title, items: entries.filter(entry => entry.kind === kind) }))
          .filter(group => group.items.length),
      });
      return { name: ns.name, path: page_path(ns.uid), types: entries.map(({ name, kind, path }) => ({ name, kind, path })) };
    })
    .sort(by_name);

  return { namespaces: namespace_list, pages, resolve };
}

function compact(object) {
  for (const key of Object.keys(object)) {
    const value = object[key];
    if (value === undefined || value === null || (Array.isArray(value) && value.length === 0)) delete object[key];
  }
  return object;
}
