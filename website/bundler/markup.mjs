const entities = { lt: '<', gt: '>', amp: '&', quot: '"', apos: "'", nbsp: ' ' };

const xref_re = /<xref\s+href="([^"]*)"[^>]*>([\s\S]*?)<\/xref>/g;

const token_re =
  /<xref\s+href="([^"]*)"[^>]*>([\s\S]*?)<\/xref>|<code(?:\s[^>]*)?>([\s\S]*?)<\/code>|<a\s+href="([^"]*)"[^>]*>([\s\S]*?)<\/a>|<(\/?)(p|b|strong|i|em|ul|ol|li)>|<br\s*\/?>/g;

const block_tags = { ul: 'list', ol: 'list', li: 'listItem' };

function decode(text) {
  return text.replace(/&(#x[0-9a-f]+|#\d+|[a-z]+);/gi, (whole, name) => {
    if (name[0] === '#') {
      const code = name[1] === 'x' || name[1] === 'X' ? parseInt(name.slice(2), 16) : parseInt(name.slice(1), 10);
      return Number.isFinite(code) ? String.fromCodePoint(code) : whole;
    }
    return entities[name.toLowerCase()] ?? whole;
  });
}

function strip_tags(text) {
  return decode(text.replace(/<[^>]*>/g, '')).replace(/\s+/g, ' ').trim();
}

function is_langword(href) {
  return href.includes('/csharp/language-reference/');
}

export function markup_to_mdast(markup, resolve) {
  if (typeof markup !== 'string' || markup.trim() === '') return null;

  const root = { type: 'root', children: [] };
  const containers = [root];
  let paragraph = [];
  const stack = [{ children: paragraph }];

  const top = () => stack[stack.length - 1].children;
  const container = () => containers[containers.length - 1];

  function push_text(raw) {
    const value = decode(raw).replace(/\s+/g, ' ');
    if (!value) return;
    const target = top();
    const last = target[target.length - 1];
    if (last?.type === 'text') last.value += value;
    else target.push({ type: 'text', value });
  }

  function flush() {
    while (stack.length > 1) stack.pop();
    trim_edges(paragraph);
    if (paragraph.length) {
      if (container().type === 'list') throw new Error(`text outside a list item in "${markup.slice(0, 120)}"`);
      container().children.push({ type: 'paragraph', children: paragraph });
    }
    paragraph = [];
    stack[0] = { children: paragraph };
  }

  function open_block(tag) {
    flush();
    const type = block_tags[tag];
    if ((type === 'listItem') !== (container().type === 'list')) throw new Error(`misplaced <${tag}> in "${markup.slice(0, 120)}"`);
    const node = type === 'list' ? { type, ordered: tag === 'ol', children: [] } : { type, children: [] };
    container().children.push(node);
    containers.push(node);
  }

  function close_block(tag) {
    flush();
    if (container().type !== block_tags[tag] || (tag !== 'li' && container().ordered !== (tag === 'ol'))) {
      throw new Error(`unbalanced </${tag}> in "${markup.slice(0, 120)}"`);
    }
    containers.pop();
  }

  function push_raw(raw) {
    const tag = /<\/?[a-z][^>]*>/i.exec(raw);
    if (tag) throw new Error(`unsupported markup ${tag[0]} in "${markup.slice(0, 120)}"`);
    push_text(raw);
  }

  let last = 0;
  for (const match of markup.matchAll(token_re)) {
    push_raw(markup.slice(last, match.index));
    last = match.index + match[0].length;

    const [, xref_href, xref_text, code_text, a_href, a_text, closing, tag] = match;
    if (xref_href !== undefined) {
      top().push(xref_node(xref_href, strip_tags(xref_text), resolve));
    } else if (code_text !== undefined) {
      const inner = code_text.replace(xref_re, (whole, href, label) => xref_label(href, strip_tags(label), resolve));
      top().push({ type: 'inlineCode', value: strip_tags(inner) });
    } else if (a_href !== undefined) {
      const text = strip_tags(a_text) || a_href;
      const href = decode(a_href);
      top().push(
        is_langword(href)
          ? { type: 'inlineCode', value: text }
          : { type: 'link', url: href, children: [{ type: 'text', value: text }] },
      );
    } else if (tag === 'p') {
      flush();
    } else if (block_tags[tag]) {
      if (closing) close_block(tag);
      else open_block(tag);
    } else if (tag) {
      const kind = tag === 'b' || tag === 'strong' ? 'strong' : 'emphasis';
      if (closing) {
        if (stack.length > 1 && stack[stack.length - 1].type === kind) stack.pop();
      } else {
        const node = { type: kind, children: [] };
        top().push(node);
        stack.push(node);
      }
    } else {
      top().push({ type: 'break' });
    }
  }
  push_raw(markup.slice(last));
  flush();
  if (containers.length > 1) throw new Error(`unclosed list in "${markup.slice(0, 120)}"`);

  return root.children.length ? root : null;
}

function trim_edges(children) {
  const first = children[0];
  if (first?.type === 'text') {
    first.value = first.value.trimStart();
    if (!first.value) children.shift();
  }
  const end = children[children.length - 1];
  if (end?.type === 'text') {
    end.value = end.value.trimEnd();
    if (!end.value) children.pop();
  }
}

function xref_target(href, text, resolve) {
  const [raw_uid, query = ''] = decode(href).split('?');
  const uid = safe_decode(raw_uid);
  const params = new URLSearchParams(query);
  const target = resolve(uid);
  const label =
    text ||
    params.get('text') ||
    (params.get('displayProperty') === 'nameWithType' ? target?.name_with_type : null) ||
    target?.name ||
    uid.split('.').pop();
  return { label, url: target?.url };
}

function xref_label(href, text, resolve) {
  return xref_target(href, text, resolve).label;
}

function xref_node(href, text, resolve) {
  const { label, url } = xref_target(href, text, resolve);
  if (!url) return { type: 'inlineCode', value: label };
  return { type: 'link', url, children: [{ type: 'text', value: label }] };
}

function safe_decode(value) {
  try {
    return decodeURIComponent(value);
  } catch {
    return value;
  }
}
