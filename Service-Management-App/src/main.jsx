import { StrictMode, useCallback, useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { DataMinerAuthError, executeAutomationScript } from './dataminer/gqi.js';
import { CATALOG_SCRIPTS, fetchServiceSpecifications } from './dataminer/catalog.js';
import { fetchServiceOrderItems, fetchServiceOrders, isOpenOrder, SERVICE_ORDER_SCRIPTS } from './dataminer/serviceOrders.js';
import { fetchServiceItems, fetchServiceScripts, fetchServices, isActiveService, SERVICE_INVENTORY_SCRIPTS } from './dataminer/services.js';
import { getConnectionFromCookie, redirectToAuth } from './dataminer/session.js';
import './styles.css';

const pages = {
    overview: { label: 'Overview', icon: 'dashboard', eyebrow: 'Workspace' },
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

function LocalConnectionNotice() {
    return <div className="app-shell"><main className="connection-page"><section className="connection-card"><span className="connection-icon icon" aria-hidden="true">cloud_off</span><p className="eyebrow">Live preview</p><h1>Connect this preview to DataMiner</h1><p>This local server is in live mode and does not load sample orders. Open the deployed DataMiner page for real GQI data, or configure <code>VITE_DATAMINER_ORIGIN</code> and restart the dev server.</p><div className="connection-options"><div><strong>Real DMA</strong><span>Open the app from its DataMiner-hosted URL.</span></div><div><strong>Local sample data</strong><span>Run <code>npm run dev:mock</code> explicitly.</span></div></div></section></main></div>;
}

function PageContent({ page, param }) {
    if (page === 'settings') return <SettingsPage />;
    if (page === 'overview') return <OverviewPage />;
    if (page === 'orders') return <OrdersPage selectedId={param} />;
    if (page === 'services') return <ServicesPage selectedId={param} />;
    if (page === 'catalog') return <CatalogPage selectedId={param} />;
    return null;
}

const stateTones = { completed: 'done', acknowledged: 'active', 'in progress': 'active', held: 'attention', rejected: 'attention', failed: 'attention', 'partially failed': 'attention', cancelled: 'closed' };
function stateTone(state) { return stateTones[state.trim().toLowerCase()] || 'pending'; }
function orderReference(order) { return order.externalId || ''; }
function orderStartTimestamp(order) { const value = Date.parse(order.start || ''); return Number.isNaN(value) ? 0 : value; }
const ATTENTION_STATES = new Set(['held', 'assess cancellation', 'pending cancellation', 'partially failed', 'failed', 'rejected']);
function isAttentionOrder(order) { return ATTENTION_STATES.has(order.state.trim().toLowerCase()); }
function isCompletedOrder(order) { return order.state.trim().toLowerCase() === 'completed'; }
const priorityRank = { high: 0, medium: 1, low: 2 };
const serviceStateTones = { active: 'done', reserved: 'active', designed: 'active', new: 'pending', terminated: 'closed', retired: 'closed' };
function serviceTone(state) { return serviceStateTones[state.trim().toLowerCase()] || 'pending'; }

function useDataList(fetcher) {
    const [state, setState] = useState({ status: 'loading', items: [], error: '' });
    const latestRequest = useRef(0);
    const load = useCallback(() => {
        const request = ++latestRequest.current;
        setState(current => ({ ...current, status: 'loading', error: '' }));
        fetcher().then(items => { if (request === latestRequest.current) setState({ status: 'ready', items, error: '' }); }, error => { if (request === latestRequest.current && !(error instanceof DataMinerAuthError)) setState({ status: 'error', items: [], error: error?.message || 'Unknown error.' }); });
    }, [fetcher]);
    useEffect(() => { load(); return () => { latestRequest.current++; }; }, [load]);
    return { ...state, reload: load };
}

function OverviewMetric({ href, label, value, note, accent, icon }) {
    return <a className="overview-metric" href={href} style={{ '--metric-accent': `var(${accent})` }}><div className="overview-metric-top"><span className="overview-metric-icon icon" aria-hidden="true">{icon}</span><span className="overview-metric-label">{label}</span></div><strong>{value}</strong><span className="overview-metric-note">{note}</span><span className="overview-metric-arrow icon" aria-hidden="true">arrow_forward</span></a>;
}

function OverviewListMessage({ status, error, emptyText }) {
    if (status === 'loading') return <div className="overview-empty"><span className="icon spin" aria-hidden="true">refresh</span>Loading data…</div>;
    if (status === 'error') return <div className="overview-empty error"><span className="icon" aria-hidden="true">error</span>{error}</div>;
    return <div className="overview-empty"><span className="icon" aria-hidden="true">inbox</span>{emptyText}</div>;
}

function OverviewPage() {
    const orderData = useServiceOrders();
    const serviceData = useDataList(fetchServices);
    const catalogData = useDataList(fetchServiceSpecifications);
    const ordersReady = orderData.status === 'ready';
    const servicesReady = serviceData.status === 'ready';
    const catalogReady = catalogData.status === 'ready';
    const openOrders = orderData.orders.filter(isOpenOrder).length;
    const attentionOrders = orderData.orders.filter(isAttentionOrder).length;
    const activeServices = serviceData.items.filter(isActiveService).length;
    const totalCatalogItems = catalogData.items.reduce((total, item) => total + item.itemCount, 0);
    const recentOrders = [...orderData.orders].sort((left, right) => orderStartTimestamp(right) - orderStartTimestamp(left)).slice(0, 6);
    const reloadAll = () => { orderData.reload(); serviceData.reload(); catalogData.reload(); };
    return <main className="page-content overview-page">
        <section className="overview-hero"><div><div className="overview-heading-meta"><p className="eyebrow">Workspace overview</p><span className="overview-status"><span aria-hidden="true" />Operational view</span></div><h1>Service Management</h1><p className="description">Monitor service orders, inventory, and the catalog from one operational view.</p></div><button className="quiet-button" type="button" onClick={reloadAll} disabled={orderData.status === 'loading' || serviceData.status === 'loading' || catalogData.status === 'loading'}><span className={(orderData.status === 'loading' || serviceData.status === 'loading' || catalogData.status === 'loading') ? 'icon spin' : 'icon'} aria-hidden="true">refresh</span>Refresh workspace</button></section>
        <section className="overview-metrics" aria-label="Workspace metrics">
            <OverviewMetric href="#orders" label="Open orders" value={ordersReady ? openOrders : '—'} note={ordersReady ? `${orderData.orders.length} total orders` : 'Waiting for GQI'} accent="--palette-color4" icon="assignment" />
            <OverviewMetric href="#orders" label="Orders needing attention" value={ordersReady ? attentionOrders : '—'} note="Held, partially failed, rejected, or cancelling" accent="--palette-color3" icon="error" />
            <OverviewMetric href="#services" label="Active services" value={servicesReady ? activeServices : '—'} note={servicesReady ? `${serviceData.items.length} in inventory` : 'Waiting for GQI'} accent="--success" icon="dm_service" />
            <OverviewMetric href="#catalog" label="Catalog specifications" value={catalogReady ? catalogData.items.length : '—'} note={catalogReady ? `${totalCatalogItems} service items defined` : 'Waiting for GQI'} accent="--focus-color" icon="inventory_2" />
        </section>
        <section className="overview-columns">
            <section className="overview-panel" aria-labelledby="overview-orders-title"><div className="overview-panel-heading"><div><p className="eyebrow">Service operations</p><h2 id="overview-orders-title">Recent orders</h2></div><a className="quiet-button" href="#orders">View all <span className="icon" aria-hidden="true">arrow_forward</span></a></div>{ordersReady && orderData.orders.length > 0 ? <div className="overview-list">{recentOrders.map(order => <a className="overview-list-row" href={`#orders/${encodeURIComponent(order.id)}`} key={order.id}><span className="overview-list-icon icon" aria-hidden="true">assignment</span><span className="overview-list-main"><strong>{order.name || 'Unnamed order'}</strong><span>{[orderReference(order), order.organization].filter(Boolean).join('  ·  ') || 'No reference information'}</span></span><span className={`state-badge ${stateTone(order.state)}`}>{order.state || 'Unknown'}</span><span className="icon overview-list-arrow" aria-hidden="true">chevron_right</span></a>)}</div> : <OverviewListMessage status={orderData.status} error={orderData.error} emptyText="No service orders yet." />}</section>
            <section className="overview-panel" aria-labelledby="overview-services-title"><div className="overview-panel-heading"><div><p className="eyebrow">Service inventory</p><h2 id="overview-services-title">Services at a glance</h2></div><a className="quiet-button" href="#services">View inventory <span className="icon" aria-hidden="true">arrow_forward</span></a></div>{servicesReady && serviceData.items.length > 0 ? <div className="overview-list">{serviceData.items.slice(0, 6).map(service => <a className="overview-list-row" href="#services" key={service.id}><span className="overview-list-icon service icon" aria-hidden="true">dm_service</span><span className="overview-list-main"><strong>{service.name || 'Unnamed service'}</strong><span>{[service.serviceId, service.specification].filter(Boolean).join('  ·  ') || 'No reference information'}</span></span><span className={`state-badge ${serviceTone(service.status)}`}>{service.status || 'Unknown'}</span><span className="icon overview-list-arrow" aria-hidden="true">chevron_right</span></a>)}</div> : <OverviewListMessage status={serviceData.status} error={serviceData.error} emptyText="No services in inventory yet." />}</section>
        </section>
    </main>;
}

function useServiceItems(serviceId) {
    const fetcher = useCallback(() => serviceId ? fetchServiceItems(serviceId) : Promise.resolve([]), [serviceId]);
    return useDataList(fetcher);
}

function ServiceDetailPanel({ service, onClose }) {
    const closeButton = useRef(null);
    const itemData = useServiceItems(service.id);
    const lifecycle = ['New', 'Designed', 'Reserved', 'Active', 'Terminated', 'Retired'];
    const currentIndex = lifecycle.findIndex(state => state.toLowerCase() === service.status.trim().toLowerCase());
    useEffect(() => {
        const previous = document.activeElement;
        closeButton.current?.focus();
        const onKey = event => { if (event.key === 'Escape') onClose(); };
        document.addEventListener('keydown', onKey);
        document.body.classList.add('drawer-open');
        return () => { document.removeEventListener('keydown', onKey); document.body.classList.remove('drawer-open'); previous?.focus?.(); };
    }, [onClose]);
    const details = [['Service ID', service.serviceId], ['Service specification', service.specification], ['Service category', service.category], ['Category type', service.categoryType], ['Start time', service.start], ['End time', service.end], ['Alarm level', Number(service.alarmLevel) > 0 ? `Level ${service.alarmLevel}` : 'Clear']];
    return <div className="drawer-layer"><div className="drawer-backdrop" onClick={onClose} aria-hidden="true" /><aside className="drawer service-drawer" role="dialog" aria-modal="true" aria-labelledby="service-detail-title"><header className="service-drawer-topbar"><nav aria-label="Service detail sections"><span className="service-tab active"><span className="icon" aria-hidden="true">assignment</span>Service</span><span className="service-tab"><span className="icon" aria-hidden="true">settings</span>Configurations</span><span className="service-tab"><span className="icon" aria-hidden="true">hub</span>Topology</span><span className="service-tab"><span className="icon" aria-hidden="true">bolt</span>Operations</span></nav><button ref={closeButton} className="icon-button" type="button" aria-label="Close service details" onClick={onClose}><span className="icon">close</span></button></header><div className="service-drawer-body"><div className="service-drawer-heading"><div><p className="eyebrow">Service Info</p><h2 id="service-detail-title">{service.name || 'Unnamed service'}</h2></div><span className={`state-badge ${serviceTone(service.status)}`}>{service.status || 'Unknown'}</span></div><div className="service-lifecycle" aria-label="Service lifecycle">{lifecycle.map((state, index) => <span className={index === currentIndex ? 'service-lifecycle-step current' : index < currentIndex ? 'service-lifecycle-step complete' : 'service-lifecycle-step'} key={state}>{state}</span>)}</div><div className="service-detail-layout"><section className="service-info-card"><div className="service-info-card-heading"><h3>Service details</h3><button className="quiet-button drawer-action-button" type="button" disabled title="Service editing will be connected with the service panel"><span className="icon" aria-hidden="true">edit</span>Edit</button></div><dl className="detail-grid service-detail-grid">{details.map(([label, value]) => <div key={label}><dt>{label}</dt><dd className={label === 'DOM instance ID' ? 'mono' : ''}>{value || '—'}</dd></div>)}</dl></section><section className="service-info-card related-orders-card"><div className="service-info-card-heading"><h3>Related service orders</h3><span className="count-pill">0</span></div><div className="service-related-empty"><span className="icon" aria-hidden="true">assignment</span>No related service-order relationship is available yet.</div></section></div><section className="service-items-panel"><div className="service-items-heading"><div><p className="eyebrow">Service configuration</p><h3>Service items</h3></div><button className="primary-button" type="button" disabled title="Service item actions will be connected with the service panel"><span className="icon" aria-hidden="true">add</span>Add item</button></div>{itemData.status === 'loading' ? <div className="service-related-empty"><span className="icon spin" aria-hidden="true">refresh</span>Loading service items…</div> : itemData.status === 'error' ? <div className="service-related-empty error"><span className="icon" aria-hidden="true">error</span>{itemData.error}<button className="quiet-button" type="button" onClick={itemData.reload}>Retry</button></div> : itemData.items.length === 0 ? <div className="service-related-empty"><span className="icon" aria-hidden="true">inbox</span>Nothing to show.</div> : <div className="service-items-table-wrap"><table className="service-items-table"><thead><tr><th>Label</th><th>Service item type</th><th>Definition reference</th><th>Implementation reference name</th><th>Implementation state</th><th>Monitoring service state</th><th>Actions</th></tr></thead><tbody>{itemData.items.map(item => <tr key={item.id || item.label}><td>{item.label || '—'}</td><td>{item.type || '—'}</td><td>{item.definition || '—'}</td><td>{item.implementation || '—'}</td><td>{item.implementationState || '—'}</td><td>{item.monitoringState || '—'}</td><td><div className="service-actions"><button className="table-icon-action" type="button" disabled title="Service item actions will be connected with the service panel"><span className="icon" aria-hidden="true">edit</span></button><button className="table-icon-action danger" type="button" disabled title="Service item actions will be connected with the service panel"><span className="icon" aria-hidden="true">delete</span></button></div></td></tr>)}</tbody></table></div>}</section></div></aside></div>;
}

function useServiceScripts(serviceId) {
    const fetcher = useCallback(() => serviceId ? fetchServiceScripts(serviceId) : Promise.resolve([]), [serviceId]);
    return useDataList(fetcher);
}

function ServiceConfigurationsView({ service }) {
    return <div className="service-drawer-body"><div className="service-drawer-heading"><div><p className="eyebrow">Configurations</p><h2>Service configuration details</h2></div></div><section className="service-configuration-card"><div className="configuration-toolbar"><button className="quiet-button" type="button" disabled>Show Value Details</button><button className="quiet-button" type="button" disabled>Show Lifecycle Details</button></div><div className="configuration-version-grid"><label>Version<select disabled><option>Current version</option></select></label><button className="quiet-button" type="button" disabled>Copy</button><label>Created at<input value={service.start || '—'} readOnly /></label></div><div className="configuration-version-details"><h3>Version details</h3><label>Version name<input value="Current version" readOnly /></label><label>Description<textarea value={`Configuration for ${service.name || 'this service'}`} readOnly /></label><label>Start date<input value={service.start || '—'} readOnly /></label><label>End date<input value={service.end || '—'} readOnly /></label></div><div className="configuration-empty"><span className="icon" aria-hidden="true">tune</span>Detailed configuration parameters are not exposed by the current Service Inventory GQI source.</div></section></div>;
}

function ServiceTopologyView({ service, items }) {
    const topologyItems = items.slice(0, 6);
    return <div className="service-drawer-body"><div className="service-drawer-heading"><div><p className="eyebrow">Topology</p><h2>Service topology</h2></div></div><div className="topology-categories"><span className="active">Processing</span><span>Acquisition</span><span>Distribution</span><span>Other</span></div>{topologyItems.length > 0 ? <div className="topology-items">{topologyItems.map(item => <div className="topology-item" key={item.id || item.label}><span className="icon" aria-hidden="true">sync</span><strong>{item.label || item.definition || service.name}</strong><small>{item.type || 'Service item'}</small></div>)}</div> : <div className="configuration-empty"><span className="icon" aria-hidden="true">hub</span>No topology items are available for this service.</div>}<button className="primary-button topology-connect" type="button" disabled>Connect</button></div>;
}

function ServiceOperationsView({ service, scripts }) {
    return <div className="service-drawer-body"><div className="service-drawer-heading"><div><p className="eyebrow">Operations</p><h2>Service operations</h2></div><button className="primary-button" type="button" disabled title="Operation creation will be connected later"><span className="icon" aria-hidden="true">add</span>Add</button></div><section className="service-operations-panel"><table><thead><tr><th>Description</th><th>Script name</th></tr></thead><tbody>{scripts.status === 'loading' ? <tr><td colSpan="2">Loading operations…</td></tr> : scripts.status === 'error' ? <tr><td colSpan="2">{scripts.error}</td></tr> : scripts.items.length === 0 ? <tr><td colSpan="2">Nothing to show.</td></tr> : scripts.items.map(script => <tr key={script.name}><td>{script.description || '—'}</td><td>{script.name || '—'}</td></tr>)}</tbody></table></section></div>;
}

function TabbedServiceDetailPanel({ service, onClose, onEdit, onDelete }) {
    const closeButton = useRef(null);
    const [tab, setTab] = useState('service');
    const itemData = useServiceItems(service.id);
    const scripts = useServiceScripts(service.id);
    const lifecycle = ['New', 'Designed', 'Reserved', 'Active', 'Terminated', 'Retired'];
    const currentIndex = lifecycle.findIndex(state => state.toLowerCase() === service.status.trim().toLowerCase());
    useEffect(() => {
        const previous = document.activeElement;
        closeButton.current?.focus();
        const onKey = event => { if (event.key === 'Escape') onClose(); };
        document.addEventListener('keydown', onKey);
        document.body.classList.add('drawer-open');
        return () => { document.removeEventListener('keydown', onKey); document.body.classList.remove('drawer-open'); previous?.focus?.(); };
    }, [onClose]);
    const details = [['Service ID', service.serviceId], ['Service specification', service.specification], ['Service category', service.category], ['Category type', service.categoryType], ['Start time', service.start], ['End time', service.end], ['Alarm level', Number(service.alarmLevel) > 0 ? `Level ${service.alarmLevel}` : 'Clear']];
    const serviceView = <div className="service-drawer-body"><div className="service-drawer-heading"><div><p className="eyebrow">Service Info</p><h2 id="service-detail-title">{service.name || 'Unnamed service'}</h2></div><span className={`state-badge ${serviceTone(service.status)}`}>{service.status || 'Unknown'}</span></div><div className="service-lifecycle" aria-label="Service lifecycle">{lifecycle.map((state, index) => <span className={index === currentIndex ? 'service-lifecycle-step current' : index < currentIndex ? 'service-lifecycle-step complete' : 'service-lifecycle-step'} key={state}>{state}</span>)}</div><div className="service-detail-layout"><section className="service-info-card"><div className="service-info-card-heading"><h3>Service details</h3><div className="drawer-card-actions"><button className="quiet-button drawer-action-button" type="button" onClick={onEdit}><span className="icon" aria-hidden="true">edit</span>Edit</button><button className="danger-button drawer-action-button" type="button" onClick={onDelete}><span className="icon" aria-hidden="true">delete</span>Delete</button></div></div><dl className="detail-grid service-detail-grid">{details.map(([label, value]) => <div key={label}><dt>{label}</dt><dd className={label === 'DOM instance ID' ? 'mono' : ''}>{value || '—'}</dd></div>)}</dl></section><section className="service-info-card related-orders-card"><div className="service-info-card-heading"><h3>Related service orders</h3><span className="count-pill">0</span></div><div className="service-related-empty"><span className="icon" aria-hidden="true">assignment</span>No related service-order relationship is available yet.</div></section></div><section className="service-items-panel"><div className="service-items-heading"><div><p className="eyebrow">Service configuration</p><h3>Service items</h3></div><button className="primary-button" type="button" disabled><span className="icon" aria-hidden="true">add</span>Add item</button></div>{itemData.status === 'loading' ? <div className="service-related-empty"><span className="icon spin" aria-hidden="true">refresh</span>Loading service items…</div> : itemData.items.length === 0 ? <div className="service-related-empty"><span className="icon" aria-hidden="true">inbox</span>Nothing to show.</div> : <div className="service-items-table-wrap"><table className="service-items-table"><thead><tr><th>Label</th><th>Service item type</th><th>Definition reference</th><th>Implementation state</th><th>Monitoring service state</th></tr></thead><tbody>{itemData.items.map(item => <tr key={item.id || item.label}><td>{item.label || '—'}</td><td>{item.type || '—'}</td><td>{item.definition || '—'}</td><td>{item.implementationState || '—'}</td><td>{item.monitoringState || '—'}</td></tr>)}</tbody></table></div>}</section></div>;
    return <div className="drawer-layer"><div className="drawer-backdrop" onClick={onClose} aria-hidden="true" /><aside className="drawer service-drawer" role="dialog" aria-modal="true" aria-labelledby="service-detail-title"><header className="service-drawer-topbar"><nav aria-label="Service detail sections"><button className={tab === 'service' ? 'service-tab active' : 'service-tab'} type="button" onClick={() => setTab('service')} aria-selected={tab === 'service'}><span className="icon" aria-hidden="true">assignment</span>Service</button><button className={tab === 'configurations' ? 'service-tab active' : 'service-tab'} type="button" onClick={() => setTab('configurations')} aria-selected={tab === 'configurations'}><span className="icon" aria-hidden="true">settings</span>Configurations</button><button className={tab === 'topology' ? 'service-tab active' : 'service-tab'} type="button" onClick={() => setTab('topology')} aria-selected={tab === 'topology'}><span className="icon" aria-hidden="true">hub</span>Topology</button><button className={tab === 'operations' ? 'service-tab active' : 'service-tab'} type="button" onClick={() => setTab('operations')} aria-selected={tab === 'operations'}><span className="icon" aria-hidden="true">bolt</span>Operations</button></nav><button ref={closeButton} className="icon-button" type="button" aria-label="Close service details" onClick={onClose}><span className="icon">close</span></button></header>{tab === 'service' ? serviceView : tab === 'configurations' ? <ServiceConfigurationsView service={service} /> : tab === 'topology' ? <ServiceTopologyView service={service} items={itemData.items} /> : <ServiceOperationsView service={service} scripts={scripts} />}</aside></div>;
}

function ServicesPage({ selectedId }) {
    const { status, items: services, error, reload } = useDataList(fetchServices);
    const { state: actionState, run: runAction, dismiss: dismissAction } = useAutomationActions();
    const [search, setSearch] = useState('');
    const [statusFilter, setStatusFilter] = useState('all');
    const [sort, setSort] = useState({ key: 'default', direction: 'asc' });
    const loading = status === 'loading';
    const hasData = status === 'ready' || (loading && services.length > 0);
    const activeCount = services.filter(isActiveService).length;
    const retiredCount = services.filter(service => service.status.trim().toLowerCase() === 'retired').length;
    const alarmedCount = services.filter(service => Number(service.alarmLevel) > 0).length;
    const selectedService = selectedId ? services.find(service => service.id === selectedId) : null;
    const editService = async service => { if (await runAction(SERVICE_INVENTORY_SCRIPTS.edit, { 'DOM ID': service.id, Action: 'Edit' }, 'Opening service editor')) reload(); };
    const duplicateService = async service => { if (await runAction(SERVICE_INVENTORY_SCRIPTS.duplicate, { 'DOM ID': service.id, Action: 'Duplicate' }, 'Duplicating service')) reload(); };
    const deleteService = async service => {
        if (!window.confirm(`Delete "${service.name || 'this service'}"?`)) return;
        if (await runAction(SERVICE_INVENTORY_SCRIPTS.delete, { 'DOM ID': service.id }, 'Deleting service')) {
            if (selectedId === service.id) location.hash = 'services';
            reload();
        }
    };
    const normalizedSearch = search.trim().toLowerCase();
    const filteredServices = services.filter(service => {
        const normalizedStatus = service.status.trim().toLowerCase();
        const matchesStatus = statusFilter === 'all' || (statusFilter === 'active' && isActiveService(service)) || (statusFilter === 'retired' && normalizedStatus === 'retired');
        const searchable = [service.name, service.serviceId, service.status, service.category, service.specification].join(' ').toLowerCase();
        return matchesStatus && (!normalizedSearch || searchable.includes(normalizedSearch));
    });
    const visibleServices = [...filteredServices];
    if (sort.key !== 'default') {
        visibleServices.sort((left, right) => {
            const values = [left[sort.key] || '', right[sort.key] || ''];
            const comparison = values[0].localeCompare(values[1], undefined, { sensitivity: 'base' });
            return sort.direction === 'asc' ? comparison : -comparison;
        });
    }
    const toggleSort = key => setSort(current => current.key === key ? { key, direction: current.direction === 'asc' ? 'desc' : 'asc' } : { key, direction: 'asc' });
    const sortIndicator = key => <span className={`table-sort-icon ${sort.key === key ? sort.direction : 'muted'}`} aria-hidden="true" />;
    const sortButton = (key, label) => <button className="table-sort" type="button" onClick={() => toggleSort(key)}>{label}{sortIndicator(key)}</button>;
    let body;
    if (status === 'error') body = <div className="list-message error" role="alert"><span className="icon" aria-hidden="true">error</span><div className="list-message-text"><strong>Services could not be loaded</strong><span>{error}</span></div><button className="quiet-button" type="button" onClick={reload}>Retry</button></div>;
    else if (!hasData) body = <div className="list-message" role="status"><span className="icon spin" aria-hidden="true">refresh</span>Loading services…</div>;
    else if (services.length === 0) body = <div className="list-message" role="status"><span className="icon" aria-hidden="true">inbox</span>No services in inventory yet.</div>;
    else if (filteredServices.length === 0) body = <div className="services-empty" role="status"><span className="icon" aria-hidden="true">search_off</span><div><strong>No services match these filters</strong><span>Try a different search or status.</span></div><button className="quiet-button" type="button" onClick={() => { setSearch(''); setStatusFilter('all'); }}>Clear filters</button></div>;
    else body = <div className="services-table-wrap" aria-busy={loading}><table className="services-table"><colgroup><col className="service-icon-column" /><col className="service-id-column" /><col className="service-name-column" /><col className="service-status-column" /><col className="service-start-column" /><col className="service-end-column" /><col className="service-specification-column" /><col className="service-category-column" /><col className="service-category-type-column" /><col className="service-action-column" /></colgroup><thead><tr><th scope="col">Icon</th><th scope="col">{sortButton('serviceId', 'ID')}</th><th scope="col">{sortButton('name', 'Name')}</th><th scope="col">{sortButton('status', 'State')}</th><th scope="col">Start</th><th scope="col">End</th><th scope="col">{sortButton('specification', 'Spec')}</th><th scope="col">{sortButton('category', 'Category')}</th><th scope="col">{sortButton('categoryType', 'Type')}</th><th scope="col"><span className="sr-only">Actions</span></th></tr></thead><tbody>{visibleServices.map(service => <tr className={service.id && service.id === selectedId ? 'service-row selected' : 'service-row'} key={service.id || service.serviceId || service.name} onDoubleClick={() => { if (service.id) location.hash = `services/${encodeURIComponent(service.id)}`; }}><td><span className="service-logo">{service.logo ? <img src={service.logo} alt="" /> : <span className="icon" aria-hidden="true">dm_service</span>}</span></td><td title={service.serviceId || undefined}><span className="table-primary">{service.serviceId || '—'}</span></td><td title={service.name || undefined}><span className="table-primary">{service.name || 'Unnamed service'}</span></td><td><span className={`state-badge ${serviceTone(service.status)}`}>{service.status || 'Unknown'}</span></td><td title={service.start || undefined}><span className="table-description">{service.start || '—'}</span></td><td title={service.end || undefined}><span className="table-description">{service.end || '—'}</span></td><td title={service.specification || undefined}><span className="table-primary">{service.specification || '—'}</span></td><td title={service.category || undefined}><span className="table-primary">{service.category || '—'}</span></td><td title={service.categoryType || undefined}><span className="table-primary">{service.categoryType || '—'}</span></td><td><div className="service-actions"><button className="table-icon-action" type="button" aria-label={`Duplicate ${service.name || 'service'}`} onClick={event => { event.stopPropagation(); duplicateService(service); }} disabled={actionState?.status === 'running'} title="Duplicate service"><span className="icon" aria-hidden="true">content_copy</span></button><button className="table-row-open" type="button" aria-label={`Open details of ${service.name || 'unnamed service'}`} onClick={event => { event.stopPropagation(); location.hash = `services/${encodeURIComponent(service.id)}`; }}><span className="icon" aria-hidden="true">chevron_right</span></button></div></td></tr>)}</tbody></table></div>;
    return <main className="page-content services-page"><section className="services-hero"><div><p className="eyebrow">{pages.services.eyebrow}</p><h1>Services</h1><p className="description">Explore the services currently defined in the Service Inventory.</p></div><button className="quiet-button" type="button" onClick={reload} disabled={loading}><span className={loading ? 'icon spin' : 'icon'} aria-hidden="true">refresh</span>{loading ? 'Refreshing' : 'Refresh'}</button></section><ActionNotice state={actionState} onDismiss={dismissAction} /><section className="services-metrics" aria-label="Service inventory metrics"><button className={statusFilter === 'all' ? 'service-metric selected' : 'service-metric'} type="button" onClick={() => setStatusFilter('all')}><span className="overview-metric-top"><span className="overview-metric-icon icon" aria-hidden="true">dm_service</span><span>All services</span></span><strong>{hasData ? services.length : '—'}</strong><small>Inventory records</small></button><button className={statusFilter === 'active' ? 'service-metric selected' : 'service-metric'} type="button" onClick={() => setStatusFilter('active')}><span className="overview-metric-top"><span className="overview-metric-icon icon" aria-hidden="true">check_circle</span><span>Active</span></span><strong>{hasData ? activeCount : '—'}</strong><small>Designed, reserved, or active</small></button><button className={statusFilter === 'retired' ? 'service-metric selected' : 'service-metric'} type="button" onClick={() => setStatusFilter('retired')}><span className="overview-metric-top"><span className="overview-metric-icon icon" aria-hidden="true">pause_circle</span><span>Retired</span></span><strong>{hasData ? retiredCount : '—'}</strong><small>No longer in operation</small></button><div className="service-metric static"><span className="overview-metric-top"><span className="overview-metric-icon icon" aria-hidden="true">warning</span><span>Alarmed</span></span><strong>{hasData ? alarmedCount : '—'}</strong><small>Inventory alarm level above clear</small></div></section><section className="services-surface" aria-labelledby="services-table-title"><div className="services-toolbar"><div className="orders-toolbar-title"><div><p className="eyebrow">Service inventory</p><h2 id="services-table-title">All services</h2></div>{hasData && <span className="orders-total-copy">{services.length} {services.length === 1 ? 'service' : 'services'} in total</span>}</div><div className="orders-toolbar-controls"><label className="orders-search"><span className="icon" aria-hidden="true">search</span><span className="sr-only">Search services</span><input type="search" value={search} onChange={event => setSearch(event.target.value)} placeholder="Search services" aria-label="Search services" />{search && <button type="button" aria-label="Clear search" onClick={() => setSearch('')}><span className="icon" aria-hidden="true">close</span></button>}</label><label className="orders-filter"><span className="sr-only">Filter services by status</span><select value={statusFilter} onChange={event => setStatusFilter(event.target.value)} aria-label="Filter services by status"><option value="all">All statuses</option><option value="active">Active</option><option value="retired">Retired</option></select><span className="icon" aria-hidden="true">expand_more</span></label></div></div>{body}</section>{selectedService && <TabbedServiceDetailPanel service={selectedService} onClose={() => { location.hash = 'services'; }} onEdit={() => editService(selectedService)} onDelete={() => deleteService(selectedService)} />}</main>;
}

function SpecificationDetailPanel({ specification, onClose, onDelete }) {
    const closeButton = useRef(null);
    const itemData = useServiceItems(specification.id);
    useEffect(() => {
        const previous = document.activeElement;
        closeButton.current?.focus();
        const onKey = event => { if (event.key === 'Escape') onClose(); };
        document.addEventListener('keydown', onKey);
        document.body.classList.add('drawer-open');
        return () => { document.removeEventListener('keydown', onKey); document.body.classList.remove('drawer-open'); previous?.focus?.(); };
    }, [onClose]);
    const details = [['Created at', specification.createdAt], ['Created by', specification.createdBy], ['Last modified', specification.lastModified], ['Last modified by', specification.lastModifiedBy]];
    return <div className="drawer-layer"><div className="drawer-backdrop" onClick={onClose} aria-hidden="true" /><aside className="drawer service-drawer specification-drawer" role="dialog" aria-modal="true" aria-labelledby="specification-detail-title"><header className="service-drawer-topbar"><nav aria-label="Specification detail sections"><span className="service-tab active"><span className="icon" aria-hidden="true">assignment</span>Service Specification</span><span className="service-tab"><span className="icon" aria-hidden="true">settings</span>Configurations</span><span className="service-tab"><span className="icon" aria-hidden="true">hub</span>Topology</span></nav><button ref={closeButton} className="icon-button" type="button" aria-label="Close specification details" onClick={onClose}><span className="icon">close</span></button></header><div className="service-drawer-body"><div className="service-drawer-heading"><div><p className="eyebrow">Service Specification Info</p><h2 id="specification-detail-title">{specification.name || 'Unnamed specification'}</h2></div><div className="drawer-card-actions"><button className="quiet-button drawer-action-button" type="button" disabled title="Specification editing will be connected later"><span className="icon" aria-hidden="true">edit</span>Edit</button><button className="danger-button drawer-action-button" type="button" onClick={onDelete}><span className="icon" aria-hidden="true">delete</span>Delete</button></div></div><section className="specification-info-card"><div className="specification-description"><dt>Description</dt><dd>{specification.description || 'No description'}</dd></div><dl className="specification-meta">{details.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value || '—'}</dd></div>)}</dl></section><section className="service-items-panel specification-items-panel"><div className="service-items-heading"><div><h3>Service Items</h3></div><button className="primary-button" type="button" disabled title="Specification item actions will be connected later"><span className="icon" aria-hidden="true">add</span>Add</button></div>{itemData.status === 'loading' ? <div className="service-related-empty"><span className="icon spin" aria-hidden="true">refresh</span>Loading service items…</div> : itemData.status === 'error' ? <div className="service-related-empty error"><span className="icon" aria-hidden="true">error</span>{itemData.error}<button className="quiet-button" type="button" onClick={itemData.reload}>Retry</button></div> : itemData.items.length === 0 ? <div className="service-related-empty"><span className="icon" aria-hidden="true">inbox</span>Nothing to show.</div> : <div className="service-items-table-wrap"><table className="service-items-table"><thead><tr><th>Label</th><th>Service item type</th><th>Definition reference</th><th>Service item script</th><th>Actions</th></tr></thead><tbody>{itemData.items.map(item => <tr key={item.id || item.label}><td>{item.label || '—'}</td><td>{item.type || '—'}</td><td>{item.definition || '—'}</td><td>{item.implementation || '—'}</td><td><div className="service-actions"><button className="table-icon-action" type="button" disabled title="Specification item actions will be connected later"><span className="icon" aria-hidden="true">edit</span></button><button className="table-icon-action danger" type="button" disabled title="Specification item actions will be connected later"><span className="icon" aria-hidden="true">delete</span></button></div></td></tr>)}</tbody></table></div>}</section></div></aside></div>;
}

function TabbedSpecificationDetailPanel({ specification, onClose, onDelete }) {
    const closeButton = useRef(null);
    const [tab, setTab] = useState('specification');
    const itemData = useServiceItems(specification.id);
    useEffect(() => {
        const previous = document.activeElement;
        closeButton.current?.focus();
        const onKey = event => { if (event.key === 'Escape') onClose(); };
        document.addEventListener('keydown', onKey);
        document.body.classList.add('drawer-open');
        return () => { document.removeEventListener('keydown', onKey); document.body.classList.remove('drawer-open'); previous?.focus?.(); };
    }, [onClose]);
    const details = [['Created at', specification.createdAt], ['Created by', specification.createdBy], ['Last modified', specification.lastModified], ['Last modified by', specification.lastModifiedBy]];
    const itemsContent = itemData.status === 'loading' ? <div className="service-related-empty"><span className="icon spin" aria-hidden="true">refresh</span>Loading service items…</div> : itemData.items.length === 0 ? <div className="service-related-empty"><span className="icon" aria-hidden="true">inbox</span>Nothing to show.</div> : <div className="service-items-table-wrap"><table className="service-items-table"><thead><tr><th>Label</th><th>Service item type</th><th>Definition reference</th><th>Service item script</th></tr></thead><tbody>{itemData.items.map(item => <tr key={item.id || item.label}><td>{item.label || '—'}</td><td>{item.type || '—'}</td><td>{item.definition || '—'}</td><td>{item.implementation || '—'}</td></tr>)}</tbody></table></div>;
    const specificationView = <div className="service-drawer-body"><div className="service-drawer-heading"><div><p className="eyebrow">Service Specification Info</p><h2 id="specification-detail-title">{specification.name || 'Unnamed specification'}</h2></div><div className="drawer-card-actions"><button className="quiet-button drawer-action-button" type="button" disabled><span className="icon" aria-hidden="true">edit</span>Edit</button><button className="danger-button drawer-action-button" type="button" onClick={onDelete}><span className="icon" aria-hidden="true">delete</span>Delete</button></div></div><section className="specification-info-card"><div className="specification-description"><dt>Description</dt><dd>{specification.description || 'No description'}</dd></div><dl className="specification-meta">{details.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value || '—'}</dd></div>)}</dl></section><section className="service-items-panel specification-items-panel"><div className="service-items-heading"><div><h3>Service Items</h3></div><button className="primary-button" type="button" disabled><span className="icon" aria-hidden="true">add</span>Add</button></div>{itemsContent}</section></div>;
    const configurationView = <div className="service-drawer-body"><div className="service-drawer-heading"><div><p className="eyebrow">Configurations</p><h2>Service configuration details</h2></div></div><section className="service-configuration-card"><div className="configuration-toolbar"><button className="quiet-button" type="button" disabled>Show Value Details</button><button className="quiet-button" type="button" disabled>Show Lifecycle Details</button></div><div className="configuration-version-grid"><label>Version<select disabled><option>Current version</option></select></label><button className="quiet-button" type="button" disabled>Copy</button><label>Created at<input value={specification.createdAt || '—'} readOnly /></label></div><div className="configuration-version-details"><h3>Version details</h3><label>Version name<input value="Current version" readOnly /></label><label>Description<textarea value={specification.description || ''} readOnly /></label><label>Start date<input value="—" readOnly /></label><label>End date<input value="—" readOnly /></label></div><div className="configuration-empty"><span className="icon" aria-hidden="true">tune</span>Configuration versions, standalone parameters, and profiles are not exposed by the current Catalog GQI source.</div></section></div>;
    const topologyView = <div className="service-drawer-body"><div className="service-drawer-heading"><div><p className="eyebrow">Topology</p><h2>Service topology</h2></div></div><div className="topology-categories"><span>Acquisition</span><span className="active">Distribution</span><span>Other</span><span>Processing</span></div>{itemData.items.length > 0 ? <div className="topology-items">{itemData.items.map(item => <div className="topology-item" key={item.id || item.label}><span className="icon" aria-hidden="true">sync</span><strong>{item.label || item.definition || 'Service item'}</strong><small>{item.type || 'Service'}</small></div>)}</div> : <div className="configuration-empty"><span className="icon" aria-hidden="true">hub</span>No topology items are available for this specification.</div>}<button className="primary-button topology-connect" type="button" disabled>Connect</button></div>;
    return <div className="drawer-layer"><div className="drawer-backdrop" onClick={onClose} aria-hidden="true" /><aside className="drawer service-drawer specification-drawer" role="dialog" aria-modal="true" aria-labelledby="specification-detail-title"><header className="service-drawer-topbar"><nav aria-label="Specification detail sections"><button className={tab === 'specification' ? 'service-tab active' : 'service-tab'} type="button" onClick={() => setTab('specification')} aria-selected={tab === 'specification'}><span className="icon" aria-hidden="true">assignment</span>Service Specification</button><button className={tab === 'configurations' ? 'service-tab active' : 'service-tab'} type="button" onClick={() => setTab('configurations')} aria-selected={tab === 'configurations'}><span className="icon" aria-hidden="true">settings</span>Configurations</button><button className={tab === 'topology' ? 'service-tab active' : 'service-tab'} type="button" onClick={() => setTab('topology')} aria-selected={tab === 'topology'}><span className="icon" aria-hidden="true">hub</span>Topology</button></nav><button ref={closeButton} className="icon-button" type="button" aria-label="Close specification details" onClick={onClose}><span className="icon">close</span></button></header>{tab === 'specification' ? specificationView : tab === 'configurations' ? configurationView : topologyView}</aside></div>;
}

function CatalogPage({ selectedId }) {
    const { status, items: specifications, error, reload } = useDataList(fetchServiceSpecifications);
    const { state: actionState, run: runAction, dismiss: dismissAction } = useAutomationActions();
    const [search, setSearch] = useState('');
    const loading = status === 'loading';
    const hasData = status === 'ready' || (loading && specifications.length > 0);
    const totalItems = specifications.reduce((total, specification) => total + specification.itemCount, 0);
    const filteredSpecifications = specifications.filter(specification => specification.name.toLowerCase().includes(search.trim().toLowerCase()));
    const selectedSpecification = selectedId ? specifications.find(specification => specification.id === selectedId) : null;
    const deleteSpecification = async specification => {
        if (!window.confirm(`Delete "${specification.name || 'this specification'}"?`)) return;
        if (await runAction(CATALOG_SCRIPTS.deleteSpecification, { 'DOM ID': specification.id }, 'Deleting specification')) {
            if (selectedId === specification.id) location.hash = 'catalog';
            reload();
        }
    };
    let body;
    if (status === 'error') body = <div className="list-message error" role="alert"><span className="icon" aria-hidden="true">error</span><div className="list-message-text"><strong>Catalog could not be loaded</strong><span>{error}</span></div><button className="quiet-button" type="button" onClick={reload}>Retry</button></div>;
    else if (!hasData) body = <div className="list-message" role="status"><span className="icon spin" aria-hidden="true">refresh</span>Loading catalog…</div>;
    else if (specifications.length === 0) body = <div className="list-message" role="status"><span className="icon" aria-hidden="true">inbox</span>No service specifications yet.</div>;
    else if (filteredSpecifications.length === 0) body = <div className="services-empty" role="status"><span className="icon" aria-hidden="true">search_off</span><div><strong>No specifications match this search</strong><span>Try a different specification name.</span></div><button className="quiet-button" type="button" onClick={() => setSearch('')}>Clear search</button></div>;
    else body = <div className="catalog-table-wrap"><table className="catalog-table"><colgroup><col className="catalog-name-column" /><col className="catalog-created-column" /><col className="catalog-modified-column" /><col className="catalog-open-column" /></colgroup><thead><tr><th scope="col">Specification name</th><th scope="col">Created at</th><th scope="col">Last modified</th><th scope="col"><span className="sr-only">Open</span></th></tr></thead><tbody>{filteredSpecifications.map(specification => <tr className={specification.id === selectedId ? 'catalog-row selected' : 'catalog-row'} key={specification.id || specification.name} onDoubleClick={() => { if (specification.id) location.hash = `catalog/${encodeURIComponent(specification.id)}`; }}><td><strong>{specification.name || 'Unnamed specification'}</strong></td><td>{specification.createdAt || '—'}</td><td>{specification.lastModified || '—'}</td><td><button className="table-row-open" type="button" aria-label={`Open details of ${specification.name || 'unnamed specification'}`} onClick={() => { if (specification.id) location.hash = `catalog/${encodeURIComponent(specification.id)}`; }}><span className="icon" aria-hidden="true">chevron_right</span></button></td></tr>)}</tbody></table></div>;
    return <main className="page-content catalog-page"><section className="services-hero"><div><p className="eyebrow">{pages.catalog.eyebrow}</p><h1>Catalog</h1><p className="description">See the service specifications that shape the inventory.</p></div><div className="catalog-hero-actions"><button className="primary-button" type="button" disabled title="Specification creation will be connected later"><span className="icon" aria-hidden="true">add</span>Add specification</button><button className="quiet-button" type="button" onClick={reload} disabled={loading}><span className={loading ? 'icon spin' : 'icon'} aria-hidden="true">refresh</span>{loading ? 'Refreshing' : 'Refresh'}</button></div></section><ActionNotice state={actionState} onDismiss={dismissAction} /><section className="services-metrics" aria-label="Catalog metrics"><div className="service-metric static"><span>Specifications</span><strong>{hasData ? specifications.length : '—'}</strong><small>Reusable service definitions</small></div><div className="service-metric static"><span>Service items</span><strong>{hasData ? totalItems : '—'}</strong><small>Defined across specifications</small></div></section><section className="services-surface" aria-labelledby="catalog-table-title"><div className="services-toolbar"><div className="orders-toolbar-title"><div><p className="eyebrow">Service design</p><h2 id="catalog-table-title">Specifications</h2></div>{hasData && <span className="orders-total-copy">{specifications.length} specifications</span>}</div><div className="orders-toolbar-controls"><label className="orders-search"><span className="icon" aria-hidden="true">search</span><span className="sr-only">Search specifications</span><input type="search" value={search} onChange={event => setSearch(event.target.value)} placeholder="Search specifications" aria-label="Search specifications" />{search && <button type="button" aria-label="Clear search" onClick={() => setSearch('')}><span className="icon" aria-hidden="true">close</span></button>}</label></div></div>{body}</section>{selectedSpecification && <TabbedSpecificationDetailPanel specification={selectedSpecification} onClose={() => { location.hash = 'catalog'; }} onDelete={() => deleteSpecification(selectedSpecification)} />}</main>;
}

function useServiceOrders() {
    const [state, setState] = useState({ status: 'loading', orders: [], error: '', updatedAt: null });
    const latestRequest = useRef(0);
    const load = useCallback(() => {
        const request = ++latestRequest.current;
        setState(current => ({ ...current, status: 'loading', error: '' }));
        fetchServiceOrders().then(orders => { if (request === latestRequest.current) setState({ status: 'ready', orders, error: '', updatedAt: new Date() }); }, error => { if (request === latestRequest.current && !(error instanceof DataMinerAuthError)) setState({ status: 'error', orders: [], error: error?.message || 'Unknown error.', updatedAt: null }); });
    }, []);
    useEffect(() => { load(); return () => { latestRequest.current++; }; }, [load]);
    return { ...state, reload: load };
}

function useAutomationActions() {
    const [state, setState] = useState(null);
    const run = useCallback(async (script, values, label) => {
        setState({ status: 'running', message: `${label}…` });
        try {
            await executeAutomationScript(script, values);
            setState({ status: 'success', message: `${label} completed.` });
            return true;
        } catch (error) {
            if (error instanceof DataMinerAuthError) return false;
            setState({ status: 'error', message: error?.message || `${label} failed.` });
            return false;
        }
    }, []);
    return { state, run, dismiss: () => setState(null) };
}

function ActionNotice({ state, onDismiss }) {
    if (!state) return null;
    const icon = state.status === 'error' ? 'error' : state.status === 'success' ? 'check_circle' : 'refresh';
    return <div className={`action-notice ${state.status}`} role={state.status === 'error' ? 'alert' : 'status'}><span className={state.status === 'running' ? 'icon spin' : 'icon'} aria-hidden="true">{icon}</span><span>{state.message}</span><button className="icon-button" type="button" aria-label="Dismiss notification" onClick={onDismiss}><span className="icon" aria-hidden="true">close</span></button></div>;
}

function useOrderItems(orderId, refreshKey = 0) {
    const [state, setState] = useState({ status: 'loading', items: [], error: '' });
    const [attempt, setAttempt] = useState(0);
    useEffect(() => {
        let active = true;
        setState({ status: 'loading', items: [], error: '' });
        fetchServiceOrderItems(orderId).then(items => { if (active) setState({ status: 'ready', items, error: '' }); }, error => { if (active && !(error instanceof DataMinerAuthError)) setState({ status: 'error', items: [], error: error?.message || 'Unknown error.' }); });
        return () => { active = false; };
    }, [orderId, attempt, refreshKey]);
    return { ...state, retry: () => setAttempt(value => value + 1) };
}

function OrderItems({ orderId, refreshKey, onAdd, onEdit, onDelete, onSelect, busy }) {
    const { status, items, error, retry } = useOrderItems(orderId, refreshKey);
    let body;
    if (status === 'error') body = <div className="list-message error" role="alert"><span className="icon" aria-hidden="true">error</span><div className="list-message-text"><strong>Order items could not be loaded</strong><span>{error}</span></div><button className="quiet-button" type="button" onClick={retry}>Retry</button></div>;
    else if (status === 'loading') body = <div className="list-message" role="status"><span className="icon spin" aria-hidden="true">refresh</span>Loading order items…</div>;
    else if (items.length === 0) body = <div className="list-message" role="status"><span className="icon" aria-hidden="true">inbox</span>This order has no items.</div>;
    else body = <ol className="item-list">{items.map((item, index) => <li className="order-item selectable" key={item.id || index} tabIndex={item.id ? 0 : -1} role={item.id ? 'button' : undefined} onClick={() => onSelect(item)} onKeyDown={event => { if (item.id && (event.key === 'Enter' || event.key === ' ')) { event.preventDefault(); onSelect(item); } }}><div className="order-item-head"><div className="order-item-title"><span className="order-item-name">{item.name || 'Unnamed item'}</span></div><span className={`state-badge ${stateTone(item.status)}`}>{item.status || 'Unknown'}</span></div><dl className="item-facts">{[['Action', item.action], ['Category', item.category], ['Specification', item.specification], ['Service', item.service], ['Start', item.start], ['End', item.end]].map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value || '—'}</dd></div>)}</dl>{item.description && <p className="order-item-description">{item.description}</p>}<div className="order-item-actions"><button className="item-action-button" type="button" onClick={event => { event.stopPropagation(); onEdit(item); }} disabled={busy}><span className="icon" aria-hidden="true">edit</span>Edit</button><button className="item-action-button danger" type="button" onClick={event => { event.stopPropagation(); onDelete(item); }} disabled={busy}><span className="icon" aria-hidden="true">delete</span>Delete</button><button className="initialize-button" type="button" disabled title="The service-initialization script will be connected later"><span className="icon" aria-hidden="true">add_circle</span>Initialize service<span className="coming-soon">Soon</span></button></div></li>)}</ol>;
    return <section className="drawer-section" aria-labelledby="order-items-title"><div className="drawer-section-heading"><div className="drawer-section-title"><h3 id="order-items-title">Order items</h3>{status === 'ready' && <span className="count-pill" data-testid="order-item-count">{items.length}</span>}</div><button className="primary-button add-item-button" type="button" onClick={onAdd} disabled={busy}><span className="icon" aria-hidden="true">add</span>Add item</button></div>{body}</section>;
}

function ServiceOrderItemDetailPanel({ item, onClose, onEdit }) {
    const closeButton = useRef(null);
    const [tab, setTab] = useState('item');
    const lifecycle = ['New', 'Acknowledged', 'In Progress', 'Completed'];
    const currentIndex = lifecycle.findIndex(state => state.toLowerCase() === item.status.trim().toLowerCase());
    useEffect(() => {
        const previous = document.activeElement;
        closeButton.current?.focus();
        const onKey = event => { if (event.key === 'Escape') onClose(); };
        document.addEventListener('keydown', onKey);
        return () => { document.removeEventListener('keydown', onKey); previous?.focus?.(); };
    }, [onClose]);
    const details = [['Name', item.name], ['Action', item.action], ['State', item.status], ['Specification', item.specification], ['Service', item.service], ['Category', item.category], ['Start time', item.start], ['End time', item.end], ['Description', item.description]];
    return <div className="drawer-layer item-drawer-layer"><div className="drawer-backdrop" onClick={onClose} aria-hidden="true" /><aside className="drawer service-drawer item-detail-drawer" role="dialog" aria-modal="true" aria-labelledby="item-detail-title"><header className="service-drawer-topbar"><nav aria-label="Service order item sections"><button className={tab === 'item' ? 'service-tab active' : 'service-tab'} type="button" onClick={() => setTab('item')} aria-selected={tab === 'item'}><span className="icon" aria-hidden="true">assignment</span>Service Item</button><button className={tab === 'config' ? 'service-tab active' : 'service-tab'} type="button" onClick={() => setTab('config')} aria-selected={tab === 'config'}><span className="icon" aria-hidden="true">settings</span>Configurations</button></nav><button ref={closeButton} className="icon-button" type="button" aria-label="Close service order item details" onClick={onClose}><span className="icon">close</span></button></header>{tab === 'item' ? <div className="service-drawer-body"><div className="service-drawer-heading"><div><p className="eyebrow">Service Order Item</p><h2 id="item-detail-title">{item.name || 'Unnamed item'}</h2></div><button className="quiet-button drawer-action-button" type="button" onClick={onEdit}><span className="icon" aria-hidden="true">edit</span>Edit</button></div><div className="service-lifecycle item-lifecycle" aria-label="Service order item lifecycle">{lifecycle.map((state, index) => <span className={index === currentIndex ? 'service-lifecycle-step current' : index < currentIndex ? 'service-lifecycle-step complete' : 'service-lifecycle-step'} key={state}>{state}</span>)}</div><section className="service-info-card item-info-card"><dl className="detail-grid service-detail-grid">{details.map(([label, value]) => <div key={label} className={label === 'Description' || label === 'DOM instance ID' ? 'wide' : ''}><dt>{label}</dt><dd className={label === 'DOM instance ID' ? 'mono' : ''}>{value || '—'}</dd></div>)}</dl></section><div className="item-transition-actions"><button className="primary-button" type="button" disabled>Acknowledge</button><button className="quiet-button" type="button" disabled>Reject</button></div></div> : <div className="service-drawer-body"><div className="service-drawer-heading"><div><p className="eyebrow">Service Order Item Configurations</p><h2 id="item-detail-title">Configuration details</h2></div></div><section className="item-configuration-card"><h3>Service configuration details</h3><div className="configuration-toolbar"><button className="quiet-button" type="button" disabled>Show Value Details</button><button className="quiet-button" type="button" disabled>Show Lifecycle Details</button></div><div className="configuration-empty"><span className="icon" aria-hidden="true">tune</span>Configuration values are not exposed by the current service-order-item GQI source.</div></section></div>}</aside></div>;
}

function OrderDetailPanel({ order, onClose, onRunAction, onOrdersChanged, onDeleteOrder, actionState }) {
    const closeButton = useRef(null);
    const [itemsRefreshKey, setItemsRefreshKey] = useState(0);
    const [selectedItem, setSelectedItem] = useState(null);
    const busy = actionState?.status === 'running';
    useEffect(() => {
        const previous = document.activeElement;
        closeButton.current?.focus();
        const onKey = event => { if (event.key === 'Escape') onClose(); };
        document.addEventListener('keydown', onKey);
        document.body.classList.add('drawer-open');
        return () => { document.removeEventListener('keydown', onKey); document.body.classList.remove('drawer-open'); previous?.focus?.(); };
    }, [onClose]);
    const facts = [['External ID', order.externalId], ['Priority', order.priority], ['Related organization', order.organization], ['State', order.state]];
    const runOrderEdit = async () => { if (await onRunAction(SERVICE_ORDER_SCRIPTS.addOrder, { 'DOM ID': order.id, Action: 'Edit' }, 'Order editor')) onOrdersChanged(); };
    const runItemAction = async (script, values, label) => { if (await onRunAction(script, values, label)) setItemsRefreshKey(value => value + 1); };
    const editItem = item => runItemAction(SERVICE_ORDER_SCRIPTS.addItem, { 'DOM ID': order.id, 'Service Order Item ID': item.id, Action: 'Edit' }, 'Order-item editor');
    const deleteItem = item => { if (window.confirm(`Delete "${item.name || 'this order item'}"?`)) runItemAction(SERVICE_ORDER_SCRIPTS.deleteItem, { 'DOM ID': order.id, 'Service Order Item ID': item.id }, 'Deleting order item'); };
    const addItem = () => runItemAction(SERVICE_ORDER_SCRIPTS.addItem, { 'DOM ID': order.id, Action: 'Add' }, 'New order item');
    return <div className="drawer-layer"><div className="drawer-backdrop" onClick={onClose} aria-hidden="true" /><aside className="drawer" role="dialog" aria-modal="true" aria-labelledby="order-detail-title"><header className="drawer-header"><div className="drawer-title"><p className="eyebrow">Service order</p><h2 id="order-detail-title">{order.name || 'Unnamed order'}</h2><div className="drawer-tags"><span className={`state-badge ${stateTone(order.state)}`}>{order.state || 'Unknown'}</span>{order.priority && <span className="order-priority">{order.priority} priority</span>}{order.organization && <span className="order-priority">{order.organization}</span>}</div></div><div className="drawer-header-actions"><button className="quiet-button drawer-action-button" type="button" onClick={runOrderEdit} disabled={busy}><span className="icon" aria-hidden="true">edit</span>Edit</button><button className="danger-button drawer-action-button" type="button" onClick={() => onDeleteOrder(order)} disabled={busy}><span className="icon" aria-hidden="true">delete</span>Delete</button><button ref={closeButton} className="icon-button" type="button" aria-label="Close order details" onClick={onClose}><span className="icon">close</span></button></div></header><div className="drawer-body"><section className="drawer-section" aria-labelledby="order-info-title"><div className="drawer-section-heading"><h3 id="order-info-title">Service order details</h3></div><dl className="detail-grid">{facts.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value || '—'}</dd></div>)}<div className="wide"><dt>Description</dt><dd>{order.description || 'No description'}</dd></div><div className="wide"><dt>DOM instance ID</dt><dd className="mono">{order.id || '—'}</dd></div></dl></section><OrderItems orderId={order.id} refreshKey={itemsRefreshKey} onAdd={addItem} onEdit={editItem} onDelete={deleteItem} onSelect={setSelectedItem} busy={busy} /></div></aside>{selectedItem && <ServiceOrderItemDetailPanel item={selectedItem} onClose={() => setSelectedItem(null)} onEdit={() => editItem(selectedItem)} />}</div>;
}

function OrdersPage({ selectedId }) {
    const { status, orders, error, updatedAt, reload } = useServiceOrders();
    const { state: actionState, run: runAction, dismiss: dismissAction } = useAutomationActions();
    const [search, setSearch] = useState('');
    const [statusFilter, setStatusFilter] = useState('all');
    const [sort, setSort] = useState({ key: 'default', direction: 'asc' });
    const loading = status === 'loading';
    const hasData = status === 'ready' || (loading && orders.length > 0);
    const openCount = orders.filter(isOpenOrder).length;
    const attentionCount = orders.filter(isAttentionOrder).length;
    const completedCount = orders.filter(isCompletedOrder).length;
    const selectedOrder = selectedId ? orders.find(order => order.id === selectedId) : null;
    const isPreviewData = import.meta.env.MODE === 'mock' || import.meta.env.VITE_USE_MOCK === 'true';
    const closeDetails = useCallback(() => { location.hash = 'orders'; }, []);
    const createOrder = async () => { if (await runAction(SERVICE_ORDER_SCRIPTS.addOrder, { Action: 'Add' }, 'New service-order editor')) reload(); };
    const deleteOrder = async order => {
        if (!window.confirm(`Delete "${order.name || 'this service order'}"?`)) return;
        if (await runAction(SERVICE_ORDER_SCRIPTS.deleteOrder, { 'DOM ID': order.id }, 'Deleting service order')) {
            closeDetails();
            reload();
        }
    };
    const normalizedSearch = search.trim().toLowerCase();
    const filteredOrders = orders.filter(order => {
        const matchesStatus = statusFilter === 'all' || (statusFilter === 'open' && isOpenOrder(order)) || (statusFilter === 'attention' && isAttentionOrder(order)) || (statusFilter === 'completed' && isCompletedOrder(order));
        const searchable = [order.name, order.externalId, order.organization, order.description, order.state, order.priority].join(' ').toLowerCase();
        return matchesStatus && (!normalizedSearch || searchable.includes(normalizedSearch));
    });
    const visibleOrders = [...filteredOrders];
    if (sort.key !== 'default') {
        visibleOrders.sort((left, right) => {
            const values = {
                name: [left.name, right.name],
                organization: [left.organization, right.organization],
                priority: [priorityRank[left.priority.trim().toLowerCase()] ?? 99, priorityRank[right.priority.trim().toLowerCase()] ?? 99],
                state: [left.state, right.state],
            }[sort.key];
            const comparison = typeof values[0] === 'number' ? values[0] - values[1] : values[0].localeCompare(values[1], undefined, { sensitivity: 'base' });
            return sort.direction === 'asc' ? comparison : -comparison;
        });
    }
    const toggleSort = key => setSort(current => current.key === key ? { key, direction: current.direction === 'asc' ? 'desc' : 'asc' } : { key, direction: 'asc' });
    const clearFilters = () => { setSearch(''); setStatusFilter('all'); };
    const refreshLabel = updatedAt ? `Updated ${new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' }).format(updatedAt)}` : status === 'loading' ? 'Loading live data' : 'Not refreshed yet';
    const sortIndicator = key => <span className={`table-sort-icon ${sort.key === key ? sort.direction : 'muted'}`} aria-hidden="true" />;
    const sortButton = (key, label) => <button className="table-sort" type="button" onClick={() => toggleSort(key)}>{label}{sortIndicator(key)}</button>;
    const openOrder = order => { if (order.id) location.hash = `orders/${encodeURIComponent(order.id)}`; };
    const onOrderKeyDown = (event, order) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); openOrder(order); } };
    let body;
    if (status === 'error') body = <div className="list-message error" role="alert"><span className="icon" aria-hidden="true">error</span><div className="list-message-text"><strong>Service orders could not be loaded</strong><span>{error}</span></div><button className="quiet-button" type="button" onClick={reload}><span className="icon" aria-hidden="true">refresh</span>Retry</button></div>;
    else if (!hasData) body = <div className="list-message" role="status"><span className="icon spin" aria-hidden="true">refresh</span>Loading service orders…</div>;
    else if (orders.length === 0) body = <div className="list-message" role="status"><span className="icon" aria-hidden="true">inbox</span>No service orders yet.</div>;
    else if (filteredOrders.length === 0) body = <div className="orders-empty" role="status"><span className="icon" aria-hidden="true">search_off</span><div><strong>No orders match these filters</strong><span>Try a different search or status.</span></div><button className="quiet-button" type="button" onClick={clearFilters}>Clear filters</button></div>;
    else body = <div className="orders-table-wrap" aria-busy={loading}><table className="orders-table"><colgroup><col className="order-column" /><col className="organization-column" /><col className="priority-column" /><col className="state-column" /><col className="description-column" /><col className="action-column" /></colgroup><thead><tr><th scope="col">{sortButton('name', 'Order')}</th><th scope="col">{sortButton('organization', 'Organization')}</th><th scope="col">{sortButton('priority', 'Priority')}</th><th scope="col">{sortButton('state', 'State')}</th><th scope="col">Description</th><th scope="col"><span className="sr-only">Open</span></th></tr></thead><tbody>{visibleOrders.map(order => <tr className={order.id && order.id === selectedId ? 'order-row selected' : 'order-row'} key={order.id || order.name} onDoubleClick={() => openOrder(order)}><td><div className="table-order-cell"><span className="order-icon"><span className="icon" aria-hidden="true">assignment</span></span><span className="table-cell-main"><strong>{order.name || 'Unnamed order'}</strong><span>{orderReference(order) || 'No external ID'}</span></span></div></td><td><span className="table-primary">{order.organization || '—'}</span></td><td><span className={`priority-chip ${order.priority.trim().toLowerCase() || 'none'}`}><span className="priority-dot" aria-hidden="true" />{order.priority ? `${order.priority} priority` : 'No priority'}</span></td><td><span className={`state-badge ${stateTone(order.state)}`}>{order.state || 'Unknown'}</span></td><td><span className="table-description" title={order.description || undefined}>{order.description || 'No description'}</span></td><td className="table-action"><button className="table-row-open" type="button" aria-label={`Open details of ${order.name || 'unnamed order'}`} onClick={() => openOrder(order)}><span className="icon" aria-hidden="true">chevron_right</span></button></td></tr>)}</tbody></table></div>;
    return <main className="page-content orders-page">
        <section className="orders-hero">
            <div className="orders-heading"><div className="orders-eyebrow"><p className="eyebrow">{pages.orders.eyebrow}</p><span className={isPreviewData ? 'live-indicator preview' : 'live-indicator'}><span aria-hidden="true" />{isPreviewData ? 'Preview data' : 'DataMiner data'}</span></div><h1>Orders</h1><p className="description">A clear view of every service order, from request to completion.</p></div>
            <div className="orders-header-actions"><span className="orders-update"><span className="icon" aria-hidden="true">schedule</span>{refreshLabel}</span><button className="quiet-button orders-refresh" type="button" onClick={reload} disabled={loading}><span className={loading ? 'icon spin' : 'icon'} aria-hidden="true">refresh</span>{loading ? 'Refreshing' : 'Refresh'}</button><button className="primary-button orders-new" type="button" onClick={createOrder} disabled={actionState?.status === 'running'}><span className="icon" aria-hidden="true">add</span>New order</button></div>
        </section>
        <ActionNotice state={actionState} onDismiss={dismissAction} />
        <section className="order-stats" aria-label="Orders overview"><button className={statusFilter === 'all' ? 'order-stat selected' : 'order-stat'} type="button" aria-pressed={statusFilter === 'all'} onClick={() => setStatusFilter('all')}><span className="overview-metric-top"><span className="overview-metric-icon icon" aria-hidden="true">assignment</span><span className="order-stat-label">All orders</span></span><strong>{hasData ? orders.length : '–'}</strong><span className="order-stat-note">Total in workspace</span></button><button className={statusFilter === 'open' ? 'order-stat selected' : 'order-stat'} type="button" aria-pressed={statusFilter === 'open'} onClick={() => setStatusFilter('open')}><span className="overview-metric-top"><span className="overview-metric-icon icon" aria-hidden="true">play_circle</span><span className="order-stat-label">Open orders</span></span><strong data-testid="open-orders">{hasData ? openCount : '–'}</strong><span className="order-stat-note">{hasData ? 'Still in progress' : 'Waiting for data'}</span></button><button className={statusFilter === 'attention' ? 'order-stat selected' : 'order-stat'} type="button" aria-pressed={statusFilter === 'attention'} onClick={() => setStatusFilter('attention')}><span className="overview-metric-top"><span className="overview-metric-icon icon" aria-hidden="true">error</span><span className="order-stat-label">Orders needing attention</span></span><strong>{hasData ? attentionCount : '–'}</strong><span className="order-stat-note">Held, failed, rejected, or cancelling</span></button><button className={statusFilter === 'completed' ? 'order-stat selected' : 'order-stat'} type="button" aria-pressed={statusFilter === 'completed'} onClick={() => setStatusFilter('completed')}><span className="overview-metric-top"><span className="overview-metric-icon icon" aria-hidden="true">check_circle</span><span className="order-stat-label">Completed</span></span><strong>{hasData ? completedCount : '–'}</strong><span className="order-stat-note">Successfully delivered</span></button></section>
        <section className="orders-surface" aria-labelledby="orders-table-title"><div className="orders-toolbar"><div className="orders-toolbar-title"><div><p className="eyebrow">Service orders</p><h2 id="orders-table-title">All orders</h2></div>{hasData && <span className="orders-total-copy">{orders.length} {orders.length === 1 ? 'order' : 'orders'} in total</span>}</div><div className="orders-toolbar-controls"><label className="orders-search"><span className="icon" aria-hidden="true">search</span><span className="sr-only">Search orders</span><input type="search" value={search} onChange={event => setSearch(event.target.value)} placeholder="Search orders" aria-label="Search orders" />{search && <button type="button" aria-label="Clear search" onClick={() => setSearch('')}><span className="icon" aria-hidden="true">close</span></button>}</label><label className="orders-filter"><span className="sr-only">Filter orders by status</span><select value={statusFilter} onChange={event => setStatusFilter(event.target.value)} aria-label="Filter orders by status"><option value="all">All statuses</option><option value="open">Open</option><option value="attention">Needs attention</option><option value="completed">Completed</option></select><span className="icon" aria-hidden="true">expand_more</span></label></div></div>{body}</section>
        {selectedOrder && <OrderDetailPanel key={selectedOrder.id} order={selectedOrder} onClose={closeDetails} onRunAction={runAction} onOrdersChanged={reload} onDeleteOrder={deleteOrder} actionState={actionState} />}
    </main>;
}

function SettingsPage() {
    const { theme, setTheme, resolvedTheme } = useTheme();
    return <main className="page-content"><div className="page-heading"><div><p className="eyebrow">Workspace</p><h1>Settings</h1><p className="description">Tune the workspace to fit the way you work.</p></div></div><section className="settings-section"><div><p className="eyebrow">Appearance</p><h2>Theme</h2><p className="setting-description">System mode follows your operating system preference automatically.</p></div><div className="theme-picker" role="group" aria-label="Theme"><button className={theme === 'system' ? 'theme-choice active' : 'theme-choice'} type="button" onClick={() => setTheme('system')}><span className="icon">contrast</span>System</button><button className={theme === 'light' ? 'theme-choice active' : 'theme-choice'} type="button" onClick={() => setTheme('light')}><span className="icon">light_mode</span>Light</button><button className={theme === 'dark' ? 'theme-choice active' : 'theme-choice'} type="button" onClick={() => setTheme('dark')}><span className="icon">dark_mode</span>Dark</button></div><p className="resolved-theme">Currently using <strong>{resolvedTheme}</strong> appearance</p></section><section className="settings-section compact"><div><p className="eyebrow">Session</p><h2>DataMiner connection</h2><p className="setting-description">Your session is managed by DataMiner authentication.</p></div><button className="quiet-button" type="button" onClick={() => location.replace(`/auth/logout?url=${encodeURIComponent(location.pathname + location.search)}`)}>Sign out <span className="icon">logout</span></button></section></main>;
}

function parseRoute() { const [page, ...rest] = location.hash.slice(1).split('/'); const key = pages[page] ? page : 'overview'; return { page: key, param: key === page ? decodeURIComponent(rest.join('/')) : '' }; }

function App() {
    const [route, setRoute] = useState(parseRoute);
    const { theme, setTheme } = useTheme();
    useEffect(() => { const onHash = () => setRoute(parseRoute()); window.addEventListener('hashchange', onHash); return () => window.removeEventListener('hashchange', onHash); }, []);
    const page = route.page;
    return <div className="app-shell"><header className="app-header"><a className="brand" href="#overview" aria-label="Service Management home"><img src="./references/dataminer-logo.png" alt="DataMiner" /><span className="brand-divider" /><strong>Service Management</strong></a><nav className="main-nav" aria-label="Primary navigation">{Object.entries(pages).filter(([key]) => key !== 'overview').map(([key, value]) => <a className={page === key ? 'nav-link active' : 'nav-link'} href={`#${key}`} key={key}><span className="icon">{value.icon}</span>{value.label}</a>)}</nav><div className="header-actions"><ThemeMenu theme={theme} setTheme={setTheme} /><button className="avatar" type="button" aria-label="Account">SM</button></div></header><PageContent page={page} param={route.param} /></div>;
}

async function start() {
    const isMockMode = import.meta.env.MODE === 'mock' || import.meta.env.VITE_USE_MOCK === 'true';
    const isUnconfiguredLocalLive = import.meta.env.DEV && !isMockMode && !import.meta.env.VITE_DATAMINER_ORIGIN;
    const hasCookie = Boolean(getConnectionFromCookie());
    // Reuse the root when the dev server hot-reloads this module.
    const container = document.getElementById('root');
    container.reactRoot ??= createRoot(container);
    if (isUnconfiguredLocalLive) {
        container.reactRoot.render(<StrictMode><LocalConnectionNotice /></StrictMode>);
        return;
    }
    if (!hasCookie || !(await verifySession())) { redirectToAuth(); return; }
    container.reactRoot.render(<StrictMode><App /></StrictMode>);
}
start().catch(() => redirectToAuth());