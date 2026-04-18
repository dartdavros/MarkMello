// markdown.jsx — minimal, dependency-free Markdown → React renderer
// Handles: h1-h6, paragraphs, bold/italic/code, links, inline code, code blocks,
// ul/ol lists, blockquotes, hr, tables, task lists.

function parseInline(text, keyPrefix = 'i') {
  // Order matters. We build tokens then render.
  const nodes = [];
  let rest = text;
  let k = 0;
  const push = (n) => nodes.push(<React.Fragment key={`${keyPrefix}-${k++}`}>{n}</React.Fragment>);

  // Regex for inline tokens
  const patterns = [
    { re: /`([^`]+)`/, render: (m) => <code className="mm-inline-code">{m[1]}</code> },
    { re: /\*\*([^*]+)\*\*/, render: (m) => <strong>{parseInline(m[1], keyPrefix + '-b')}</strong> },
    { re: /\*([^*]+)\*/, render: (m) => <em>{parseInline(m[1], keyPrefix + '-i')}</em> },
    { re: /\[([^\]]+)\]\(([^)]+)\)/, render: (m) => <a href={m[2]} className="mm-link">{m[1]}</a> },
  ];

  while (rest.length) {
    let earliest = null;
    for (const p of patterns) {
      const m = rest.match(p.re);
      if (m && (earliest === null || m.index < earliest.m.index)) {
        earliest = { m, p };
      }
    }
    if (!earliest) { push(rest); break; }
    if (earliest.m.index > 0) push(rest.slice(0, earliest.m.index));
    push(earliest.p.render(earliest.m));
    rest = rest.slice(earliest.m.index + earliest.m[0].length);
  }
  return nodes;
}

function renderMarkdown(source) {
  const lines = source.replace(/\r\n/g, '\n').split('\n');
  const blocks = [];
  let i = 0;
  let key = 0;

  while (i < lines.length) {
    const line = lines[i];

    // Empty line → skip
    if (!line.trim()) { i++; continue; }

    // Horizontal rule
    if (/^\s*---+\s*$/.test(line)) {
      blocks.push(<hr key={key++} className="mm-hr" />);
      i++;
      continue;
    }

    // Code block (fenced)
    if (/^```/.test(line)) {
      const lang = line.replace(/^```/, '').trim();
      const codeLines = [];
      i++;
      while (i < lines.length && !/^```/.test(lines[i])) {
        codeLines.push(lines[i]); i++;
      }
      i++; // skip closing ```
      blocks.push(
        <pre key={key++} className="mm-pre" data-lang={lang}>
          <code>{codeLines.join('\n')}</code>
        </pre>
      );
      continue;
    }

    // Heading
    const h = line.match(/^(#{1,6})\s+(.*)$/);
    if (h) {
      const level = h[1].length;
      const Tag = `h${level}`;
      blocks.push(
        <Tag key={key++} className={`mm-h mm-h${level}`}>
          {parseInline(h[2], `h${key}`)}
        </Tag>
      );
      i++;
      continue;
    }

    // Blockquote
    if (/^>\s?/.test(line)) {
      const qLines = [];
      while (i < lines.length && /^>\s?/.test(lines[i])) {
        qLines.push(lines[i].replace(/^>\s?/, ''));
        i++;
      }
      blocks.push(
        <blockquote key={key++} className="mm-quote">
          {parseInline(qLines.join(' '), `q${key}`)}
        </blockquote>
      );
      continue;
    }

    // Table
    if (/\|/.test(line) && i + 1 < lines.length && /^[\s|:-]+$/.test(lines[i + 1])) {
      const headerCells = line.split('|').map(s => s.trim()).filter(Boolean);
      i += 2;
      const rows = [];
      while (i < lines.length && /\|/.test(lines[i]) && lines[i].trim()) {
        rows.push(lines[i].split('|').map(s => s.trim()).filter(Boolean));
        i++;
      }
      blocks.push(
        <table key={key++} className="mm-table">
          <thead>
            <tr>{headerCells.map((c, j) => <th key={j}>{parseInline(c, `th${key}-${j}`)}</th>)}</tr>
          </thead>
          <tbody>
            {rows.map((r, ri) => (
              <tr key={ri}>{r.map((c, ci) => <td key={ci}>{parseInline(c, `td${key}-${ri}-${ci}`)}</td>)}</tr>
            ))}
          </tbody>
        </table>
      );
      continue;
    }

    // Unordered list
    if (/^\s*[-*]\s+/.test(line)) {
      const items = [];
      while (i < lines.length && /^\s*[-*]\s+/.test(lines[i])) {
        const m = lines[i].match(/^\s*[-*]\s+(.*)$/);
        items.push(m[1]);
        i++;
      }
      blocks.push(
        <ul key={key++} className="mm-ul">
          {items.map((it, j) => {
            const task = it.match(/^\[([ xX])\]\s+(.*)$/);
            if (task) {
              return (
                <li key={j} className="mm-task">
                  <span className={`mm-check ${task[1].toLowerCase() === 'x' ? 'checked' : ''}`} />
                  {parseInline(task[2], `ul${key}-${j}`)}
                </li>
              );
            }
            return <li key={j}>{parseInline(it, `ul${key}-${j}`)}</li>;
          })}
        </ul>
      );
      continue;
    }

    // Ordered list
    if (/^\s*\d+\.\s+/.test(line)) {
      const items = [];
      while (i < lines.length && /^\s*\d+\.\s+/.test(lines[i])) {
        const m = lines[i].match(/^\s*\d+\.\s+(.*)$/);
        items.push(m[1]);
        i++;
      }
      blocks.push(
        <ol key={key++} className="mm-ol">
          {items.map((it, j) => <li key={j}>{parseInline(it, `ol${key}-${j}`)}</li>)}
        </ol>
      );
      continue;
    }

    // Paragraph — collect until blank line or block starter
    const pLines = [line];
    i++;
    while (i < lines.length && lines[i].trim()
      && !/^(#{1,6}\s|>|```|\s*[-*]\s|\s*\d+\.\s|\s*---+\s*$)/.test(lines[i])) {
      pLines.push(lines[i]);
      i++;
    }
    blocks.push(
      <p key={key++} className="mm-p">
        {parseInline(pLines.join(' '), `p${key}`)}
      </p>
    );
  }

  return blocks;
}

window.renderMarkdown = renderMarkdown;
