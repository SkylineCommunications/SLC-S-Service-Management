import { StrictMode, useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import './styles.css';

const pages = {
    orders: { label: 'Orders', icon: 'assignment', eyebrow: 'Service operations' },
    services: { label: 'Services', icon: 'dm_service', eyebrow: 'Service operations' },
    catalog: { label: 'Catalog', icon: 'inventory_2', eyebrow: 'Service design' },
    settings: { label: 'Settings', icon: 'settings', eyebrow: 'Workspace' },
};

function readTheme() { return localStorage.getItem('service-management-theme') || 'system'; }

function useTheme() {
    const [theme, setTheme] = useState(readTheme);
    const [systemDark, setSystemDark] = useState(() => window.matchMedia('(prefers-color-scheme: dark)').matches);
    const resolvedTheme = theme === 'system' ? (systemDark ? 'dark' : 'light') : theme;
    useEffect(() => { localStorage.setItem('service-management-theme', theme); document.documentElement.dataset.theme = resolvedTheme; }, [theme, resolvedTheme]);
    useEffect(() => { const media = window.matchMedia('(prefers-color-scheme: dark)'); const update = () => setSystemDark(media.matches); media.addEventListener('change', update); return () => media.removeEventListener('change', update); }, []);
    return { theme, setTheme, resolvedTheme };
}

function getConnectionFromCookie() { const match = document.cookie.match(/(?:^|;\s*)DMAConnection=([^;]+)/); return match ? decodeURIComponent(match[1]) : null; }
function redirectToAuth() { const target = `${location.pathname}${location.search}`; location.replace(`/auth/?url=${encodeURIComponent(target)}`); }

async function verifySession() {
    const connection = getConnectionFromCookie();
    if (!connection) return false;
    const response = await fetch('/API/v1/Json.asmx/IsConnectionAlive', { method: 'POST', headers: { 'Content-Type': 'application/json' }, credentials: 'same-origin', body: JSON.stringify({ connection }) });
    if (response.status === 401 || response.status === 403) return false;
    const payload = await response.json();
    if (response.status === 500 && payload?.ExceptionType?.includes('NoConnectionWebApiException')) return false;
    return response.ok;
}

function ThemeMenu({ theme, setTheme }) {
    const [open, setOpen] = useState(false);
    const labels = { system: 'System', light: 'Light', dark: 'Dark' };
    return <div className="theme-control"><button className="icon-button" type="button" aria-label="Choose theme" aria-expanded={open} onClick={() => setOpen(!open)}><span className="icon">{theme === 'dark' ? 'dark_mode' : theme === 'light' ? 'light_mode' : 'contrast'}</span></button>{open && <div className="theme-menu" role="menu">{Object.entries(labels).map(([value, label]) => <button className={theme === value ? 'theme-option selected' : 'theme-option'} key={value} role="menuitem" type="button" onClick={() => { setTheme(value); setOpen(false); }}><span className="icon">{value === 'dark' ? 'dark_mode' : value === 'light' ? 'light_mode' : 'contrast'}</span>{label}{theme === value && <span className="icon option-check">check</span>}</button>)}</div>}</div>;
}

function PageContent({ page }) {
    const content = {
        orders: { title: 'Orders', description: 'Coordinate service delivery from request to completion.', metric: '12', metricLabel: 'Open orders', accent: 'palette-color4', items: ['SO-1042  ·  Network refresh', 'SO-1041  ·  Studio expansion', 'SO-1038  ·  Backup link'] },
        services: { title: 'Services', description: 'Keep an operational view of active customer services.', metric: '28', metricLabel: 'Active services', accent: 'palette-color2', items: ['Broadcast contribution', 'Cloud playout', 'Managed connectivity'] },
        catalog: { title: 'Catalog', description: 'Shape reusable service specifications for your teams.', metric: '16', metricLabel: 'Service specifications', accent: 'palette-color3', items: ['Video transport', 'Event production', 'Managed IP transit'] },
    };
    if (page === 'settings') return <SettingsPage />;
    const data = content[page];
    return <main className="page-content"><div className="page-heading"><div><p className="eyebrow">{pages[page].eyebrow}</p><h1>{data.title}</h1><p className="description">{data.description}</p></div><button className="primary-button" type="button"><span className="icon">add</span>New {page === 'catalog' ? 'specification' : page.slice(0, -1)}</button></div><section className="overview-grid" aria-label={`${data.title} overview`}><div className="metric-panel" style={{ '--metric-accent': `var(--${data.accent})` }}><span className="metric-label">{data.metricLabel}</span><strong>{data.metric}</strong><span className="trend"><span className="icon">trending_up</span> 8% this month</span></div><div className="signal-panel"><span className="panel-kicker">Workspace pulse</span><strong>Everything is in rhythm</strong><p>Use this space to connect your service workflow with the work happening today.</p></div></section><section className="list-section"><div className="section-heading"><div><p className="eyebrow">Recent activity</p><h2>Keep moving</h2></div><button className="quiet-button" type="button">View all <span className="icon">arrow_forward</span></button></div><div className="activity-list">{data.items.map((item, index) => <div className="activity-row" key={item}><span className="activity-number">0{index + 1}</span><span>{item}</span><span className="icon row-arrow">arrow_forward</span></div>)}</div></section></main>;
}

function SettingsPage() {
    const { theme, setTheme, resolvedTheme } = useTheme();
    return <main className="page-content"><div className="page-heading"><div><p className="eyebrow">Workspace</p><h1>Settings</h1><p className="description">Tune the workspace to fit the way you work.</p></div></div><section className="settings-section"><div><p className="eyebrow">Appearance</p><h2>Theme</h2><p className="setting-description">System mode follows your operating system preference automatically.</p></div><div className="theme-picker" role="group" aria-label="Theme"><button className={theme === 'system' ? 'theme-choice active' : 'theme-choice'} type="button" onClick={() => setTheme('system')}><span className="icon">contrast</span>System</button><button className={theme === 'light' ? 'theme-choice active' : 'theme-choice'} type="button" onClick={() => setTheme('light')}><span className="icon">light_mode</span>Light</button><button className={theme === 'dark' ? 'theme-choice active' : 'theme-choice'} type="button" onClick={() => setTheme('dark')}><span className="icon">dark_mode</span>Dark</button></div><p className="resolved-theme">Currently using <strong>{resolvedTheme}</strong> appearance</p></section><section className="settings-section compact"><div><p className="eyebrow">Session</p><h2>DataMiner connection</h2><p className="setting-description">Your session is managed by DataMiner authentication.</p></div><button className="quiet-button" type="button" onClick={() => location.replace(`/auth/logout?url=${encodeURIComponent(location.pathname + location.search)}`)}>Sign out <span className="icon">logout</span></button></section></main>;
}

function App() {
    const [page, setPage] = useState(location.hash.slice(1) || 'orders');
    const { theme, setTheme } = useTheme();
    useEffect(() => { const onHash = () => setPage(location.hash.slice(1) || 'orders'); window.addEventListener('hashchange', onHash); return () => window.removeEventListener('hashchange', onHash); }, []);
    return <div className="app-shell"><header className="app-header"><a className="brand" href="#orders" aria-label="Service Management home"><img src="./references/dataminer-logo.svg" alt="DataMiner" /><span className="brand-divider" /><strong>Service Management</strong></a><nav className="main-nav" aria-label="Primary navigation">{Object.entries(pages).map(([key, value]) => <a className={page === key ? 'nav-link active' : 'nav-link'} href={`#${key}`} key={key}><span className="icon">{value.icon}</span>{value.label}</a>)}</nav><div className="header-actions"><ThemeMenu theme={theme} setTheme={setTheme} /><button className="avatar" type="button" aria-label="Account">SM</button></div></header><PageContent page={pages[page] ? page : 'orders'} /></div>;
}

async function start() { const hasCookie = Boolean(getConnectionFromCookie()); if (!hasCookie || !(await verifySession())) { redirectToAuth(); return; } createRoot(document.getElementById('root')).render(<StrictMode><App /></StrictMode>); }
start().catch(() => redirectToAuth());