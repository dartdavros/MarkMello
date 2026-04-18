// components.jsx — MarkMello UI components
// Pure React, no external dependencies.

// Hooks accessed via React.* to avoid redeclaration across Babel script files.

/* ---------- Icons (inline SVG, 14px grid) ---------- */
const Icon = ({ d, size = 14, stroke = 1.5 }) => (
  <svg className="mm-icon" width={size} height={size} viewBox="0 0 14 14"
       fill="none" stroke="currentColor" strokeWidth={stroke}
       strokeLinecap="round" strokeLinejoin="round">
    {typeof d === 'string' ? <path d={d} /> : d}
  </svg>
);

const Icons = {
  sun:  <Icon d="M7 2.5v1M7 10.5v1M2.5 7h1M10.5 7h1M3.7 3.7l.7.7M9.6 9.6l.7.7M3.7 10.3l.7-.7M9.6 4.4l.7-.7" />,
  moon: <Icon d="M10.5 8.5A4 4 0 1 1 5.5 3.5 3.2 3.2 0 0 0 10.5 8.5Z" />,
  edit: <Icon d="M2.5 11.5L5 11l6-6-2-2-6 6-.5 2.5zM8.5 3.5l2 2" />,
  eye:  <Icon d={<><path d="M1.5 7s2-4 5.5-4 5.5 4 5.5 4-2 4-5.5 4S1.5 7 1.5 7Z"/><circle cx="7" cy="7" r="1.5"/></>} />,
  settings: <Icon d={<><circle cx="7" cy="7" r="1.5"/><path d="M11.3 8.5 12 9l-1 1.7-.9-.3a3.7 3.7 0 0 1-1.4.8L8.5 12h-3l-.2-.8a3.7 3.7 0 0 1-1.4-.8L3 10.7 2 9l.7-.5a3.7 3.7 0 0 1 0-1.8L2 6.2 3 4.5l.9.3a3.7 3.7 0 0 1 1.4-.8L5.5 3h3l.2.8a3.7 3.7 0 0 1 1.4.8l.9-.3 1 1.7-.7.5a3.7 3.7 0 0 1 0 1.8Z"/></>} />,
  close: <Icon d="M3 3l8 8M11 3l-8 8" />,
  min:   <Icon d="M2.5 7h9" />,
  max:   <Icon d={<rect x="2.5" y="2.5" width="9" height="9" rx="0.5"/>} />,
  file:  <Icon d="M4 1.5h4l2.5 2.5v8.5h-6.5z M8 1.5v2.5h2.5" />,
  folder:<Icon d="M1.5 4.5v7h11v-6h-5.5l-1.5-1.5z" />,
  panel: <Icon d={<><rect x="1.5" y="2.5" width="11" height="9" rx="1"/><path d="M5.5 2.5v9"/></>} />,
  plus:  <Icon d="M7 3v8M3 7h8" />,
  chevronDown: <Icon d="M3.5 5.5l3.5 3 3.5-3" />,
  bold:  <Icon d="M4 2.5h3.5a2 2 0 0 1 0 4H4zM4 6.5h4a2 2 0 0 1 0 4H4z" stroke={2}/>,
  italic:<Icon d="M6 2.5h4M4 11.5h4M8 2.5l-2 9" />,
  link:  <Icon d="M6 8L8 6M5.5 9.5l-1 1a2.1 2.1 0 0 1-3-3l1-1M8.5 4.5l1-1a2.1 2.1 0 0 1 3 3l-1 1"/>,
  code:  <Icon d="M5 4.5L2.5 7 5 9.5M9 4.5L11.5 7 9 9.5"/>,
  list:  <Icon d="M5 4h7M5 7h7M5 10h7M2.5 4h.01M2.5 7h.01M2.5 10h.01"/>,
  quote: <Icon d="M3 4.5v4a1.5 1.5 0 0 1-1.5 1.5M7 4.5v4a1.5 1.5 0 0 1-1.5 1.5M1.5 4.5h3M5.5 4.5h3"/>,
};

/* ---------- Windows-style title bar ---------- */
function TitleBar({ fileName, dirty }) {
  return (
    <div className="mm-titlebar">
      <div className="mm-titlebar-title">
        {fileName && <span className="mm-dot" />}
        {fileName ? (
          <>
            <span className="mm-title-name">{fileName}{dirty ? ' •' : ''}</span>
            <span className="mm-title-app">— MarkMello</span>
          </>
        ) : (
          <span className="mm-title-app">MarkMello</span>
        )}
      </div>
      <div className="mm-wincontrols">
        <button aria-label="Minimize" tabIndex={-1}>{Icons.min}</button>
        <button aria-label="Maximize" tabIndex={-1}>{Icons.max}</button>
        <button className="close" aria-label="Close" tabIndex={-1}>{Icons.close}</button>
      </div>
    </div>
  );
}

/* ---------- Hover-revealed top toolbar ---------- */
function TopBar({ theme, onThemeToggle, editMode, onEditToggle, onSettingsToggle, settingsOpen }) {
  return (
    <div className="mm-top-bar" data-force={settingsOpen ? 'true' : 'false'}>
      <button className="mm-ghostbtn" onClick={onThemeToggle}
              title={theme === 'light' ? 'Switch to dark' : 'Switch to light'}>
        {theme === 'light' ? Icons.moon : Icons.sun}
      </button>
      <button
        className={`mm-ghostbtn ${editMode ? 'active' : ''}`}
        onClick={onEditToggle}
        title={editMode ? 'Exit edit mode (Ctrl+E)' : 'Edit (Ctrl+E)'}>
        {editMode ? Icons.eye : Icons.edit}
        <span>{editMode ? 'Reading' : 'Edit'}</span>
      </button>
      <button
        className={`mm-ghostbtn ${settingsOpen ? 'active' : ''}`}
        onClick={onSettingsToggle}
        title="Reading preferences (Ctrl+,)">
        {Icons.settings}
      </button>
    </div>
  );
}

/* ---------- Settings panel (animated reveal) ---------- */
function SettingsPanel({ open, prefs, setPrefs, onClose }) {
  const panelRef = React.useRef(null);

  if (!open) return null;

  return (
    <div
      ref={panelRef}
      className="mm-settings-panel"
      style={{
        animation: 'mmPanelIn 220ms cubic-bezier(.2,.8,.2,1) both',
      }}>
      <style>{`
        @keyframes mmPanelIn {
          from { opacity: 0; transform: translateY(-6px) scale(0.96); }
          to   { opacity: 1; transform: translateY(0) scale(1); }
        }
      `}</style>
      <div className="mm-settings-head">
        <span className="mm-settings-title">Reading</span>
        <button className="mm-settings-close" onClick={onClose} aria-label="Close">
          {Icons.close}
        </button>
      </div>
      <div className="mm-settings-body">

        <div className="mm-setting-row">
          <div>
            <div className="mm-setting-label">Font</div>
            <div className="mm-setting-hint">Document typeface</div>
          </div>
          <div className="mm-segmented">
            {['serif', 'sans', 'mono'].map(f => (
              <button key={f} className={prefs.font === f ? 'active' : ''}
                      onClick={() => setPrefs({ ...prefs, font: f })}>
                {f === 'serif' ? 'Serif' : f === 'sans' ? 'Sans' : 'Mono'}
              </button>
            ))}
          </div>
        </div>

        <div className="mm-setting-row">
          <div>
            <div className="mm-setting-label">Size</div>
            <div className="mm-setting-hint">Base font size</div>
          </div>
          <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
            <input type="range" className="mm-slider" min={14} max={24} step={1}
                   value={prefs.size}
                   onChange={e => setPrefs({ ...prefs, size: +e.target.value })} />
            <span className="mm-slider-val">{prefs.size}px</span>
          </div>
        </div>

        <div className="mm-setting-row">
          <div>
            <div className="mm-setting-label">Line height</div>
            <div className="mm-setting-hint">Reading comfort</div>
          </div>
          <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
            <input type="range" className="mm-slider" min={1.4} max={2.0} step={0.05}
                   value={prefs.lineHeight}
                   onChange={e => setPrefs({ ...prefs, lineHeight: +e.target.value })} />
            <span className="mm-slider-val">{prefs.lineHeight.toFixed(2)}</span>
          </div>
        </div>

        <div className="mm-setting-row">
          <div>
            <div className="mm-setting-label">Width</div>
            <div className="mm-setting-hint">Measure of a line</div>
          </div>
          <div className="mm-segmented">
            {[['narrow', 580], ['medium', 720], ['wide', 860]].map(([k, v]) => (
              <button key={k} className={prefs.width === v ? 'active' : ''}
                      onClick={() => setPrefs({ ...prefs, width: v })}>
                {k[0].toUpperCase() + k.slice(1)}
              </button>
            ))}
          </div>
        </div>

        <div className="mm-setting-row">
          <div>
            <div className="mm-setting-label">Chrome</div>
            <div className="mm-setting-hint">Interface visibility</div>
          </div>
          <div className="mm-segmented">
            {['zero', 'minimal'].map(c => (
              <button key={c} className={prefs.chrome === c ? 'active' : ''}
                      onClick={() => setPrefs({ ...prefs, chrome: c })}>
                {c[0].toUpperCase() + c.slice(1)}
              </button>
            ))}
          </div>
        </div>

      </div>
    </div>
  );
}

/* ---------- Editor toolbar ---------- */
function EditorToolbar({ wrap }) {
  const b = (icon, label) => (
    <button title={label} onClick={() => wrap && wrap(label)}>{icon}</button>
  );
  return (
    <div className="mm-editor-toolbar">
      {b(Icons.bold, 'bold')}
      {b(Icons.italic, 'italic')}
      {b(Icons.code, 'code')}
      <span className="sep" />
      {b(Icons.link, 'link')}
      {b(Icons.list, 'list')}
      {b(Icons.quote, 'quote')}
      <span className="label">Source</span>
    </div>
  );
}

Object.assign(window, {
  Icons, TitleBar, TopBar, SettingsPanel, EditorToolbar,
});
