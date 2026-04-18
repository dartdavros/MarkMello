// app.jsx — MarkMello main app

const { useState, useEffect, useRef } = React;

const DEFAULTS = /*EDITMODE-BEGIN*/{
  "theme": "light",
  "font": "serif",
  "size": 18,
  "lineHeight": 1.7,
  "width": 720,
  "chrome": "zero"
}/*EDITMODE-END*/;

const RECENT = [
  { name: 'architecture.md', date: '2 hours ago', path: '~/Documents/markmello/' },
  { name: 'constitution.md', date: 'Yesterday', path: '~/Documents/markmello/' },
  { name: 'vision.md', date: 'Yesterday', path: '~/Documents/markmello/' },
  { name: 'meeting-notes.md', date: '3 days ago', path: '~/Desktop/' },
  { name: 'recipe-ragu.md', date: 'Last week', path: '~/Documents/personal/' },
];

function App() {
  const [prefs, setPrefs] = useState(() => {
    try {
      const saved = JSON.parse(localStorage.getItem('mm.prefs') || 'null');
      return { ...DEFAULTS, ...(saved || {}) };
    } catch { return DEFAULTS; }
  });
  const [source, setSource] = useState(null);
  const [fileName, setFileName] = useState(null);
  const [editMode, setEditMode] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [splitRatio, setSplitRatio] = useState(0.45);
  const [dragging, setDragging] = useState(false);
  const [progress, setProgress] = useState(0);
  const [showDrop, setShowDrop] = useState(false);
  const bodyRef = useRef(null);
  const scrollRef = useRef(null);
  const textareaRef = useRef(null);

  // Load sample on first mount
  useEffect(() => {
    fetch('sample.md').then(r => r.text()).then(t => {
      setSource(t);
      setFileName('README.md');
    }).catch(() => {});
  }, []);

  // Persist prefs
  useEffect(() => {
    localStorage.setItem('mm.prefs', JSON.stringify(prefs));
    // Also push to host if in tweaks
    try {
      window.parent.postMessage({ type: '__edit_mode_set_keys', edits: prefs }, '*');
    } catch {}
  }, [prefs]);

  // Theme
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', prefs.theme);
  }, [prefs.theme]);

  // Reading progress
  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    const onScroll = () => {
      const max = el.scrollHeight - el.clientHeight;
      setProgress(max > 0 ? (el.scrollTop / max) * 100 : 0);
    };
    el.addEventListener('scroll', onScroll);
    onScroll();
    return () => el.removeEventListener('scroll', onScroll);
  }, [source, editMode]);

  // Keyboard shortcuts
  useEffect(() => {
    const onKey = (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'e') {
        e.preventDefault();
        setEditMode(v => !v);
      }
      if ((e.ctrlKey || e.metaKey) && e.key === ',') {
        e.preventDefault();
        setSettingsOpen(v => !v);
      }
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'o') {
        e.preventDefault();
        document.getElementById('mm-file-input')?.click();
      }
      if (e.key === 'Escape') {
        setSettingsOpen(false);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  // Close settings on outside click
  useEffect(() => {
    if (!settingsOpen) return;
    const onClick = (e) => {
      if (e.target.closest('.mm-settings-panel')) return;
      if (e.target.closest('[data-settings-trigger]')) return;
      setSettingsOpen(false);
    };
    window.addEventListener('mousedown', onClick);
    return () => window.removeEventListener('mousedown', onClick);
  }, [settingsOpen]);

  // Listen for edit-mode host activation
  useEffect(() => {
    const onMsg = (e) => {
      if (!e.data || !e.data.type) return;
      if (e.data.type === '__activate_edit_mode') {
        setSettingsOpen(true);
      }
      if (e.data.type === '__deactivate_edit_mode') {
        setSettingsOpen(false);
      }
    };
    window.addEventListener('message', onMsg);
    try { window.parent.postMessage({ type: '__edit_mode_available' }, '*'); } catch {}
    return () => window.removeEventListener('message', onMsg);
  }, []);

  // Split dragging
  useEffect(() => {
    if (!dragging) return;
    const onMove = (e) => {
      const rect = bodyRef.current.getBoundingClientRect();
      const r = (e.clientX - rect.left) / rect.width;
      setSplitRatio(Math.min(0.8, Math.max(0.2, r)));
    };
    const onUp = () => setDragging(false);
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
    return () => {
      window.removeEventListener('mousemove', onMove);
      window.removeEventListener('mouseup', onUp);
    };
  }, [dragging]);

  // Drop handler
  useEffect(() => {
    const onOver = (e) => { e.preventDefault(); setShowDrop(true); };
    const onLeave = (e) => { if (e.target === document || e.relatedTarget === null) setShowDrop(false); };
    const onDrop = async (e) => {
      e.preventDefault(); setShowDrop(false);
      const file = e.dataTransfer?.files?.[0];
      if (file) {
        const text = await file.text();
        setSource(text);
        setFileName(file.name);
        setDirty(false);
        setEditMode(false);
      }
    };
    window.addEventListener('dragover', onOver);
    window.addEventListener('dragleave', onLeave);
    window.addEventListener('drop', onDrop);
    return () => {
      window.removeEventListener('dragover', onOver);
      window.removeEventListener('dragleave', onLeave);
      window.removeEventListener('drop', onDrop);
    };
  }, []);

  const onOpenFile = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const text = await file.text();
    setSource(text);
    setFileName(file.name);
    setDirty(false);
    setEditMode(false);
  };

  const wrap = (kind) => {
    const ta = textareaRef.current;
    if (!ta) return;
    const s = ta.selectionStart, e = ta.selectionEnd;
    const before = source.slice(0, s);
    const sel = source.slice(s, e) || {
      bold: 'bold text', italic: 'italic text', code: 'code',
      link: 'link', list: 'item', quote: 'quoted text',
    }[kind];
    const after = source.slice(e);
    let wrapped = sel;
    if (kind === 'bold') wrapped = `**${sel}**`;
    else if (kind === 'italic') wrapped = `*${sel}*`;
    else if (kind === 'code') wrapped = `\`${sel}\``;
    else if (kind === 'link') wrapped = `[${sel}](url)`;
    else if (kind === 'list') wrapped = `\n- ${sel}`;
    else if (kind === 'quote') wrapped = `\n> ${sel}`;
    const next = before + wrapped + after;
    setSource(next);
    setDirty(true);
    requestAnimationFrame(() => {
      ta.focus();
      ta.setSelectionRange(s + wrapped.length, s + wrapped.length);
    });
  };

  const toggleTheme = () => setPrefs({ ...prefs, theme: prefs.theme === 'light' ? 'dark' : 'light' });
  const wordCount = source ? source.trim().split(/\s+/).filter(Boolean).length : 0;
  const readTime = Math.max(1, Math.round(wordCount / 220));

  // Compute doc style
  const docStyle = {
    '--mm-col': `${prefs.width}px`,
    '--mm-font-size': `${prefs.size}px`,
    '--mm-line-height': prefs.lineHeight,
  };

  const editorPaneWidth = editMode && bodyRef.current
    ? `${splitRatio * 100}%`
    : undefined;

  return (
    <div className="mm-window">
      <TitleBar fileName={fileName} dirty={dirty} />

      {/* Tiny reading progress (only in viewer) */}
      {!editMode && (
        <div className="mm-progress" style={{ width: `${progress}%` }} />
      )}

      {/* Hover-reveal top controls */}
      <TopBar
        theme={prefs.theme}
        onThemeToggle={toggleTheme}
        editMode={editMode}
        onEditToggle={() => setEditMode(v => !v)}
        onSettingsToggle={() => setSettingsOpen(v => !v)}
        settingsOpen={settingsOpen}
      />
      <div data-settings-trigger style={{ display: 'none' }} />

      {settingsOpen && (
        <SettingsPanel
          open={settingsOpen}
          prefs={prefs}
          setPrefs={setPrefs}
          onClose={() => setSettingsOpen(false)}
        />
      )}

      <div className="mm-body" ref={bodyRef}>
        {editMode && source !== null && (
          <>
            <div className="mm-editor-pane" style={{ width: editorPaneWidth }}>
              <EditorToolbar wrap={wrap} />
              <textarea
                ref={textareaRef}
                className="mm-editor-textarea"
                value={source}
                onChange={e => { setSource(e.target.value); setDirty(true); }}
                spellCheck={false}
              />
            </div>
            <div
              className={`mm-divider ${dragging ? 'dragging' : ''}`}
              onMouseDown={() => setDragging(true)}
            />
          </>
        )}

        {source !== null ? (
          <div className="mm-doc-scroll" ref={scrollRef}>
            <div className={`mm-doc mm-doc-${prefs.font}`} style={docStyle}>
              {window.renderMarkdown(source)}
            </div>
          </div>
        ) : (
          <Welcome onOpen={() => document.getElementById('mm-file-input').click()} />
        )}
      </div>

      {/* Bottom status bar (hover-reveal) */}
      <div className="mm-statusbar">
        <div className="mm-statusbar-group">
          {fileName && (
            <span className="mm-statusbar-item">
              {Icons.file}
              <span style={{ color: 'var(--mm-text-soft)' }}>{fileName}</span>
            </span>
          )}
          <span className="mm-statusbar-item">{wordCount.toLocaleString()} words</span>
          <span className="mm-statusbar-item">{readTime} min read</span>
        </div>
        <div className="mm-statusbar-group">
          <span className="mm-statusbar-item">
            <span className="kbd">Ctrl</span><span className="kbd">O</span>&nbsp;open
          </span>
          <span className="mm-statusbar-item">
            <span className="kbd">Ctrl</span><span className="kbd">E</span>&nbsp;{editMode ? 'read' : 'edit'}
          </span>
          <span className="mm-statusbar-item">
            <span className="kbd">Ctrl</span><span className="kbd">,</span>&nbsp;prefs
          </span>
        </div>
      </div>

      <input id="mm-file-input" type="file" accept=".md,.markdown,.txt"
             style={{ display: 'none' }} onChange={onOpenFile} />

      <div className={`mm-dropzone ${showDrop ? 'active' : ''}`}>
        Drop your Markdown file to open
      </div>
    </div>
  );
}

function Welcome({ onOpen }) {
  return (
    <div className="mm-welcome">
      <div className="mm-welcome-inner">
        <div className="mm-welcome-logo">Mark<em>Mello</em></div>
        <div className="mm-welcome-tag">A quiet place to read Markdown.</div>
        <div className="mm-welcome-actions">
          <button className="mm-welcome-btn" onClick={onOpen}>Open file…</button>
          <button className="mm-welcome-btn secondary">New document</button>
        </div>

        <div className="mm-welcome-recent">
          <div className="mm-welcome-recent-head">Recent</div>
          {RECENT.map((r, i) => (
            <div key={i} className="mm-recent-item">
              <span className="name">{r.name}</span>
              <span className="date">{r.date}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
