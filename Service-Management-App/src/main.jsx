import { StrictMode, useCallback, useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { DataMinerAuthError, executeAutomationScript } from './dataminer/gqi.js';
import { CATALOG_SCRIPTS, fetchDocumentHubLogos, fetchServiceCategories, fetchServiceSpecifications, fetchServiceSpecificationConfigurations } from './dataminer/catalog.js';
import { fetchRelatedServiceOrders, fetchServiceOrderItemConfigurations, fetchServiceOrderItems, fetchServiceOrders, fetchServiceOrganizations, getOrderItemTransitions, getOrderTransitions, isOpenOrder, SERVICE_ORDER_SCRIPTS } from './dataminer/serviceOrders.js';
import { fetchMonitoringServices, fetchServiceConfigurations, fetchServiceItems, fetchServiceScripts, fetchServices, isActiveService, SERVICE_CONFIGURATION_SCRIPTS, SERVICE_INVENTORY_SCRIPTS, SERVICE_SCRIPT_SCRIPTS, SERVICE_TRANSITIONS } from './dataminer/services.js';
import { getConnectionFromCookie, redirectToAuth } from './dataminer/session.js';
import { IasDialog } from './IasDialog.jsx';
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
function transitionIcon(label) {
    const key = String(label || '').trim().toLowerCase();
    if (['acknowledge', 'complete'].includes(key)) return 'check_circle';
    if (['initialize', 'in progress'].includes(key)) return 'play_circle';
    if (['reject', 'cancel', 'confirm cancellation'].includes(key)) return 'close';
    if (['failed', 'partially failed', 'issue'].includes(key)) return 'error';
    if (['information missing'].includes(key)) return 'info';
    if (['request cancellation'].includes(key)) return 'cancel';
    return 'arrow_forward';
}
function orderReference(order) { return order.externalId || String(order.id || '').split('-')[0].toUpperCase(); }
function orderStartTimestamp(order) { return order.createdAtTimestamp || Date.parse(order.createdAt || order.start || '') || 0; }
const ATTENTION_STATES = new Set(['held', 'assess cancellation', 'pending cancellation', 'partially failed', 'failed', 'rejected']);
function isAttentionOrder(order) { return ATTENTION_STATES.has(order.state.trim().toLowerCase()); }
function isCompletedOrder(order) { return order.state.trim().toLowerCase() === 'completed'; }
const priorityRank = { high: 0, medium: 1, low: 2 };
const serviceStateTones = { active: 'done', reserved: 'active', designed: 'active', new: 'pending', terminated: 'closed', retired: 'closed' };
function serviceTone(state) { return serviceStateTones[state.trim().toLowerCase()] || 'pending'; }
function transitionStateParameter(state) { return String(state || '').trim().replace(/\s+/g, ''); }

function useDataList(fetcher) {
    const [state, setState] = useState({ status: 'loading', items: [], error: '', updatedAt: null });
    const latestRequest = useRef(0);
    const load = useCallback(({ rejectOnError = false } = {}) => {
        const request = ++latestRequest.current;
        setState(current => ({ ...current, status: 'loading', error: '' }));
        return fetcher().then(items => { if (request === latestRequest.current) setState({ status: 'ready', items, error: '', updatedAt: new Date() }); return items; }, error => { if (request === latestRequest.current && !(error instanceof DataMinerAuthError)) setState({ status: 'error', items: [], error: error?.message || 'Unknown error.', updatedAt: null }); if (rejectOnError) throw error; return []; });
    }, [fetcher]);
    const reloadAndWait = useCallback(() => load({ rejectOnError: true }), [load]);
    useEffect(() => { load(); return () => { latestRequest.current++; }; }, [load]);
    return { ...state, reload: load, reloadAndWait };
}

function PageHeaderActions({ updatedAt, loading, onRefresh, primaryAction, refreshLabel = 'Refresh' }) {
    const updatedLabel = updatedAt
        ? `Updated ${new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' }).format(updatedAt)}`
        : loading ? 'Loading data' : 'Not refreshed yet';
    return <div className={`page-header-actions ${primaryAction ? 'has-primary-action' : 'refresh-only'}`}>
        <span className="page-header-update" aria-label={`Data status: ${updatedLabel}`}>
            <span className="icon" aria-hidden="true">schedule</span>{updatedLabel}
        </span>
        <button className="quiet-button page-header-refresh" type="button" onClick={onRefresh} disabled={loading}>
            <span className={loading ? 'icon spin' : 'icon'} aria-hidden="true">refresh</span>{loading ? 'Refreshing' : refreshLabel}
        </button>
        {primaryAction && <div className="page-header-primary">{primaryAction}</div>}
    </div>;
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
    const overviewLoading = orderData.status === 'loading' || serviceData.status === 'loading' || catalogData.status === 'loading';
    const overviewUpdatedAt = orderData.updatedAt && serviceData.updatedAt && catalogData.updatedAt
        ? new Date(Math.min(orderData.updatedAt.getTime(), serviceData.updatedAt.getTime(), catalogData.updatedAt.getTime()))
        : null;
    return <main className="page-content overview-page">
        <section className="overview-hero"><div><div className="overview-heading-meta"><p className="eyebrow">Workspace overview</p><span className="overview-status"><span aria-hidden="true" />Operational view</span></div><h1>Service Management</h1><p className="description">Monitor service orders, inventory, and the catalog from one operational view.</p></div><PageHeaderActions updatedAt={overviewUpdatedAt} loading={overviewLoading} onRefresh={reloadAll} /></section>
        <section className="overview-metrics" aria-label="Workspace metrics">
            <OverviewMetric href="#orders" label="Open orders" value={ordersReady ? openOrders : '—'} note={ordersReady ? `${orderData.orders.length} total orders` : 'Waiting for GQI'} accent="--palette-color4" icon="assignment" />
            <OverviewMetric href="#orders" label="Orders needing attention" value={ordersReady ? attentionOrders : '—'} note="Held, partially failed, rejected, or cancelling" accent="--palette-color3" icon="error" />
            <OverviewMetric href="#services" label="Active services" value={servicesReady ? activeServices : '—'} note={servicesReady ? `${serviceData.items.length} in inventory` : 'Waiting for GQI'} accent="--success" icon="dm_service" />
            <OverviewMetric href="#catalog" label="Catalog specifications" value={catalogReady ? catalogData.items.length : '—'} note={catalogReady ? `${totalCatalogItems} service items defined` : 'Waiting for GQI'} accent="--focus-color" icon="inventory_2" />
        </section>
        <section className="overview-columns">
            <section className="overview-panel" aria-labelledby="overview-orders-title"><div className="overview-panel-heading"><div><p className="eyebrow">Service operations</p><h2 id="overview-orders-title">Recent orders</h2></div><a className="quiet-button" href="#orders">View all <span className="icon" aria-hidden="true">arrow_forward</span></a></div>{ordersReady && orderData.orders.length > 0 ? <div className="overview-list">{recentOrders.map(order => <a className="overview-list-row" href={`#orders/${encodeURIComponent(order.id)}`} key={order.id}><span className="overview-list-icon icon" aria-hidden="true">assignment</span><span className="overview-list-main"><strong>{order.name || 'Unnamed order'}</strong><span>{[orderReference(order), order.organization].filter(Boolean).join('  ·  ') || 'No reference information'}</span></span><span className={`state-badge ${stateTone(order.state)}`}>{order.state || 'Unknown'}</span><span className="icon overview-list-arrow" aria-hidden="true">chevron_right</span></a>)}</div> : <OverviewListMessage status={orderData.status} error={orderData.error} emptyText="No service orders yet." />}</section>
            <section className="overview-panel" aria-labelledby="overview-services-title"><div className="overview-panel-heading"><div><p className="eyebrow">Service inventory</p><h2 id="overview-services-title">Current service inventory</h2></div><a className="quiet-button" href="#services">View inventory <span className="icon" aria-hidden="true">arrow_forward</span></a></div>{servicesReady && serviceData.items.length > 0 ? <div className="overview-list">{serviceData.items.slice(0, 6).map(service => <a className="overview-list-row" href="#services" key={service.id}><span className="overview-list-icon service icon" aria-hidden="true">dm_service</span><span className="overview-list-main"><strong>{service.name || 'Unnamed service'}</strong><span>{[service.serviceId, service.specification].filter(Boolean).join('  ·  ') || 'No reference information'}</span></span><span className={`state-badge ${serviceTone(service.status)}`}>{service.status || 'Unknown'}</span><span className="icon overview-list-arrow" aria-hidden="true">chevron_right</span></a>)}</div> : <OverviewListMessage status={serviceData.status} error={serviceData.error} emptyText="No services in inventory yet." />}</section>
        </section>
    </main>;
}

function useServiceItems(serviceId) {
    const fetcher = useCallback(() => serviceId ? fetchServiceItems(serviceId) : Promise.resolve([]), [serviceId]);
    return useDataList(fetcher);
}

function useRelatedServiceOrders(serviceId) {
    const fetcher = useCallback(() => serviceId ? fetchRelatedServiceOrders(serviceId) : Promise.resolve([]), [serviceId]);
    return useDataList(fetcher);
}

function useServiceScripts(serviceId) {
    const fetcher = useCallback(() => serviceId ? fetchServiceScripts(serviceId) : Promise.resolve([]), [serviceId]);
    return useDataList(fetcher);
}

const EMPTY_SERVICE_CONFIGURATION_DATA = { versions: [], availableParameters: [], availableProfiles: [] };

function findConfigurationProfilePath(profiles, id, parentPath = []) {
    for (const profile of profiles || []) {
        const path = [...parentPath, profile];
        if (profile.id === id || profile.clientId === id) return path;
        const nestedPath = findConfigurationProfilePath(profile.nestedProfiles, id, path);
        if (nestedPath) return nestedPath;
    }
    return null;
}

function findEmptyMandatoryParameter(parameters, profiles = []) {
    const missingParameter = (parameters || []).find(parameter => parameter.mandatory && !String(parameter.value ?? '').trim());
    if (missingParameter) return missingParameter.name || missingParameter.id;
    for (const profile of profiles || []) {
        const missingProfileParameter = findEmptyMandatoryParameter(profile.parameters, profile.nestedProfiles);
        if (missingProfileParameter) return missingProfileParameter;
    }
    return '';
}

function ConfigurationParameterValueEditor({ parameter, disabled = false, onChange, ariaLabel }) {
    const currentValue = String(parameter.value ?? '');
    const options = Array.isArray(parameter.options) ? parameter.options : [];
    const isDiscrete = String(parameter.type || '').trim().toLowerCase() === 'discrete';
    const isNumber = String(parameter.type || '').trim().toLowerCase() === 'number';
    if (isNumber) {
        return <input className={disabled ? 'reusable' : ''} type="number" min={parameter.start || undefined} max={parameter.end || undefined} step={parameter.step || 'any'} value={currentValue} disabled={disabled} onChange={event => onChange(event.target.value)} aria-label={ariaLabel} />;
    }
    if (!isDiscrete) {
        return <input className={disabled ? 'reusable' : ''} value={currentValue} disabled={disabled} onChange={event => onChange(event.target.value)} aria-label={ariaLabel} />;
    }
    const hasCurrentValue = currentValue && !options.some(option => option.value === currentValue);
    return <select className={disabled ? 'reusable' : ''} value={currentValue} disabled={disabled} onChange={event => onChange(event.target.value)} aria-label={ariaLabel}>
        <option value="">Select value…</option>
        {hasCurrentValue && <option value={currentValue}>{currentValue} (current)</option>}
        {options.map(option => <option value={option.value} key={`${option.id}-${option.value}`}>{option.value}</option>)}
        {options.length === 0 && <option value="" disabled>No discrete values available</option>}
    </select>;
}

function NativeConfigurationProfileView({ profile, profilePath, profilesAtLevel, availableProfiles, availableNestedProfiles, availableParameters, onBack, onAddProfileAtLevel, onAddNestedProfileDefinition, onAddNested, onRemoveNested, onAddParameter, onUpdateParameter, onUpdateProfileName, onRemoveParameter, onOpenNested }) {
    const [nestedChoice, setNestedChoice] = useState('');
    const [parameterChoice, setParameterChoice] = useState('');
    const [profileChoice, setProfileChoice] = useState('');
    const [nestedDefinitionChoice, setNestedDefinitionChoice] = useState('');
    const depth = profilePath.length;
    const canNest = depth < 3;
    const reusable = Boolean(profile.reusable);
    const nestedProfiles = profile.nestedProfiles || [];
    const parameters = profile.parameters || [];
    const parentProfile = profilePath.length > 1 ? profilePath[profilePath.length - 2] : null;
    const levelProfiles = profilesAtLevel || [];
    const canAddProfileAtLevel = !parentProfile?.reusable;
    const profileOptions = availableProfiles;
    const nestedProfileOptions = availableNestedProfiles.filter(nested => nested.definitionId === nestedDefinitionChoice && !profilePath.some(pathProfile => (pathProfile.id || pathProfile.clientId) === nested.id) && !nestedProfiles.some(current => current.id === nested.id));
    const addProfile = () => {
        if (!profileChoice) return;
        onAddProfileAtLevel(parentProfile?.id || parentProfile?.clientId || null, profileChoice);
        setProfileChoice('');
    };
    const addNested = () => {
        if (!nestedChoice) return;
        onAddNested(profile.id || profile.clientId, nestedChoice);
        onOpenNested(nestedChoice);
        setNestedChoice('');
    };
    const addNestedDefinition = () => {
        if (!nestedDefinitionChoice) return;
        onAddNestedProfileDefinition(profile.id || profile.clientId, nestedDefinitionChoice);
        setNestedChoice('');
        setNestedDefinitionChoice('');
    };
    const addParameter = () => { if (parameterChoice) { onAddParameter(profile.id || profile.clientId, parameterChoice); setParameterChoice(''); } };
    const backLabel = parentProfile ? `Back to ${parentProfile.name || 'parent profile'}` : 'Back to configuration';
    return <div className="native-configuration-profile-view">
        <div className="configuration-profile-screen-heading">
            <button className="configuration-back-button" type="button" onClick={onBack}>
                <span className="icon" aria-hidden="true">chevron_left</span>{backLabel}
            </button>
            <div>
                <p className="eyebrow">Profile configuration</p>
                {reusable
                    ? <h2>{profile.name || 'Unnamed profile'}</h2>
                    : <input className="configuration-profile-name-input" value={profile.name || ''} onChange={event => onUpdateProfileName(profile.id || profile.clientId, event.target.value)} aria-label={`Profile name for ${profile.name || 'profile'}`} />}
                <nav className="configuration-profile-breadcrumbs" aria-label="Profile nesting path">
                    <span>Configuration</span>
                    {profilePath.map((pathProfile, index) => <span className="configuration-profile-breadcrumb" key={pathProfile.id || pathProfile.clientId || index}>
                        <span className="icon" aria-hidden="true">chevron_right</span>
                        <span aria-current={index === profilePath.length - 1 ? 'page' : undefined}>{pathProfile.name || 'Unnamed profile'}</span>
                    </span>)}
                </nav>
            </div>
        </div>
        {depth > 1 && <section className="native-configuration-profiles native-profile-level-profiles">
            <div className="service-items-heading">
                <div><p className="eyebrow">Profiles at this level</p></div>
                {canAddProfileAtLevel && <div className="configuration-add-controls">
                    <select value={profileChoice} onChange={event => setProfileChoice(event.target.value)} aria-label="Select regular profile to add at this level">
                        <option value="">Select profile…</option>
                        {profileOptions.map(candidate => <option value={candidate.id} key={candidate.id}>{candidate.name || candidate.id}</option>)}
                    </select>
                    <button className="quiet-button" type="button" onClick={addProfile} disabled={!profileChoice}>Add</button>
                </div>}
            </div>
            {levelProfiles.length === 0 ? <div className="configuration-empty">No profiles are assigned at this level.</div> : <ul className="configuration-nested-list native-profile-level-list">{levelProfiles.map(levelProfile => {
                const id = levelProfile.id || levelProfile.clientId;
                const isCurrent = id === (profile.id || profile.clientId);
                return <li key={id || `${levelProfile.definitionId}-${levelProfile.name}`}>
                    <button className="configuration-nested-chip" type="button" onClick={() => onOpenNested(id)} aria-current={isCurrent ? 'page' : undefined} aria-label={`Open ${levelProfile.name || levelProfile.id}`}>
                        <span>{levelProfile.name || levelProfile.id}</span>
                        <span className="icon" aria-hidden="true">chevron_right</span>
                    </button>
                </li>;
            })}</ul>}
        </section>}
        <section className="native-configuration-parameters native-profile-parameters">
            <div className="service-items-heading">
                <div>
                    <p className="eyebrow">Profile parameters</p>
                    {reusable && <span className="configuration-profile-readonly">Reusable values are read-only.</span>}
                </div>
                {!reusable && <div className="configuration-add-controls">
                    <select value={parameterChoice} onChange={event => setParameterChoice(event.target.value)} aria-label="Select profile parameter">
                        <option value="">Select parameter…</option>
                        {availableParameters.filter(parameter => !parameters.some(current => current.id === parameter.id)).map(parameter => <option value={parameter.id} key={`${parameter.id}-${parameter.name}`}>{parameter.name || parameter.id}</option>)}
                    </select>
                    <button className="quiet-button" type="button" onClick={addParameter} disabled={!parameterChoice}>Add</button>
                </div>}
            </div>
            {parameters.length === 0 ? <div className="configuration-empty">No profile parameters are assigned.</div> : <div className="configuration-profile-parameters-list profile-parameter-editor-list">{parameters.map(parameter => <div className="configuration-profile-parameter-row" key={parameter.valueId || parameter.id}>
                <span>{parameter.name || parameter.id}</span>
                <ConfigurationParameterValueEditor parameter={parameter} disabled={reusable} onChange={value => onUpdateParameter(profile.id || profile.clientId, parameter.id, value)} ariaLabel={`Value for ${parameter.name || parameter.id}`} />
                {!reusable && <button className="table-icon-action danger" type="button" aria-label={`Delete profile parameter ${parameter.name || parameter.id}`} onClick={() => onRemoveParameter(profile.id || profile.clientId, parameter)}><span className="icon" aria-hidden="true">delete</span></button>}
            </div>)}</div>}
        </section>
        <section className="native-configuration-profiles native-profile-nested-profiles">
            <div className="service-items-heading">
                <div>
                    <p className="eyebrow">Nested profiles</p>
                    {reusable && <span className="configuration-profile-readonly">Reusable profile structure is read-only.</span>}
                </div>
                {canNest && !reusable && <div className="nested-profile-add-controls">
                    <div className="configuration-add-controls">
                        <select value={nestedDefinitionChoice} onChange={event => { setNestedDefinitionChoice(event.target.value); setNestedChoice(''); }} aria-label="Select regular profile definition to add below this profile">
                            <option value="">Add profile definition…</option>
                            {availableProfiles.map(candidate => <option value={candidate.id} key={candidate.id}>{candidate.name || candidate.id}</option>)}
                        </select>
                        <button className="quiet-button" type="button" onClick={addNestedDefinition} disabled={!nestedDefinitionChoice}>Add</button>
                    </div>
                    {nestedDefinitionChoice && nestedProfileOptions.length > 0 && <div className="configuration-add-controls">
                        <select value={nestedChoice} onChange={event => setNestedChoice(event.target.value)} aria-label="Select reusable nested profile">
                            <option value="">Select reusable profile…</option>
                            {nestedProfileOptions.map(nested => <option value={nested.id} key={`${nested.id}-${nested.name}`}>{nested.name || nested.id}</option>)}
                        </select>
                        <button className="quiet-button" type="button" onClick={addNested} disabled={!nestedChoice}>Add</button>
                    </div>}
                </div>}
            </div>
            {nestedProfiles.length === 0
                ? <div className="configuration-empty">{canNest ? 'No nested profiles are assigned.' : 'No deeper profiles are assigned. New nested profiles are limited to level 3.'}</div>
                : <ul className="configuration-nested-list native-profile-nested-list">{nestedProfiles.map(nested => {
                const nestedId = nested.id || nested.clientId;
                return <li key={nestedId}>
                <button className="configuration-nested-chip" type="button" onClick={() => onOpenNested(nestedId)} aria-label={`Open ${nested.name || nested.id}`}>
                    <span>{nested.name || nested.id}</span>
                    <span className="icon" aria-hidden="true">chevron_right</span>
                </button>
                <button className="configuration-nested-remove" type="button" aria-label={`Remove nested profile ${nested.name || nested.id}`} onClick={() => onRemoveNested(profile.id || profile.clientId, nestedId)} disabled={reusable}>
                    <span className="icon" aria-hidden="true">delete</span>
                </button>
            </li>;})}</ul>}
        </section>
    </div>;
}

function ConfigurationProfileNodeV2({ profile, depth, availableProfiles, availableNestedProfiles, availableParameters, onAddNestedProfileDefinition, onAddNested, onRemoveNested, onAddParameter, onUpdateParameter, onUpdateProfileName, onRemoveParameter, onRemove, onOpenNested, showLifecycleDetails = false, onUpdateProfileLifecycle, initiallyExpanded = false }) {
    const [collapsed, setCollapsed] = useState(!initiallyExpanded);
    const [nestedChoice, setNestedChoice] = useState('');
    const [nestedDefinitionChoice, setNestedDefinitionChoice] = useState('');
    const [parameterChoice, setParameterChoice] = useState('');
    const canNest = depth < 3;
    const reusable = Boolean(profile.reusable);
    const parentId = profile.id || profile.clientId;
    const definitionLabel = profile.definitionName || profile.definitionId || '';
    const showDefinitionLabel = definitionLabel && definitionLabel.trim().toLowerCase() !== String(profile.name || '').trim().toLowerCase();
    const nested = profile.nestedProfiles || [];
    const parameters = profile.parameters || [];
    const reusableNestedOptions = availableNestedProfiles.filter(nestedItem => nestedItem.definitionId === nestedDefinitionChoice && nestedItem.id !== parentId && !nested.some(current => current.id === nestedItem.id));
    const addNested = () => { if (nestedChoice) { onAddNested(parentId, nestedChoice); onOpenNested(nestedChoice); setNestedChoice(''); } };
    const addNestedDefinition = () => {
        if (!nestedDefinitionChoice) return;
        onAddNestedProfileDefinition(parentId, nestedDefinitionChoice);
        setNestedDefinitionChoice('');
    };
    const addParameter = () => { if (parameterChoice) { onAddParameter(parentId, parameterChoice); setParameterChoice(''); } };
    return <article className={`configuration-profile-card profile-depth-${depth} ${collapsed ? 'collapsed' : ''}`}>
        <div className="configuration-profile-heading">
            <div className="configuration-profile-title">
                <button className="profile-collapse-button" type="button" aria-expanded={!collapsed} aria-label={`${collapsed ? 'Expand' : 'Collapse'} ${profile.name || 'profile'}`} onClick={() => setCollapsed(value => !value)}>
                    <span className="icon" aria-hidden="true">{collapsed ? 'chevron_right' : 'expand_more'}</span>
                </button>
                <div>
                    {reusable
                        ? <strong>{profile.name || 'Unnamed profile'}</strong>
                        : <input className="configuration-profile-name-input" value={profile.name || ''} onChange={event => onUpdateProfileName(profile.id || profile.clientId, event.target.value)} aria-label={`Profile name for ${profile.name || 'profile'}`} />}
                    {showDefinitionLabel && <span className="configuration-profile-detail">{definitionLabel}</span>}
                </div>
            </div>
            <button className="table-icon-action danger" type="button" aria-label={`Delete ${profile.name || 'profile'}`} onClick={() => onRemove(profile.id, profile.definitionId, profile.clientId)}>
                <span className="icon" aria-hidden="true">delete</span>
            </button>
        </div>
        {showLifecycleDetails && <div className="configuration-profile-lifecycle">
            <label>Expose at order<input type="checkbox" checked={Boolean(profile.exposeAtOrder)} onChange={event => onUpdateProfileLifecycle(parentId, 'exposeAtOrder', event.target.checked)} /></label>
            <label>Mandatory at order<input type="checkbox" checked={Boolean(profile.mandatoryAtOrder)} onChange={event => onUpdateProfileLifecycle(parentId, 'mandatoryAtOrder', event.target.checked)} /></label>
            <label>Mandatory at service<input type="checkbox" checked={Boolean(profile.mandatoryAtService)} onChange={event => onUpdateProfileLifecycle(parentId, 'mandatoryAtService', event.target.checked)} /></label>
        </div>}
        {!collapsed && <div className="configuration-profile-content">
            <div className="configuration-profile-parameters">
                <div className="configuration-subsection-heading">
                    <span className="eyebrow">Parameters</span>
                    {reusable && <span className="configuration-profile-readonly">Reusable values are read-only.</span>}
                    {!reusable && <div className="configuration-add-controls">
                        <select value={parameterChoice} onChange={event => setParameterChoice(event.target.value)} aria-label={`Select parameter for ${profile.name || 'profile'}`}>
                            <option value="">Select parameter…</option>
                            {availableParameters.filter(parameter => !parameters.some(current => current.id === parameter.id)).map(parameter => <option value={parameter.id} key={`${parameter.id}-${parameter.name}`}>{parameter.name || parameter.id}</option>)}
                        </select>
                        <button className="quiet-button" type="button" onClick={addParameter} disabled={!parameterChoice}>Add</button>
                    </div>}
                </div>
                {parameters.length === 0 ? <span className="configuration-profile-empty">No profile parameters assigned.</span> : <div className="configuration-profile-parameters-list">{parameters.map(parameter => <div className="configuration-profile-parameter-row" key={parameter.valueId || parameter.id}>
                    <span>{parameter.name || parameter.id}</span>
                    <ConfigurationParameterValueEditor parameter={parameter} disabled={reusable} onChange={value => onUpdateParameter(parentId, parameter.id, value)} ariaLabel={`Value for ${parameter.name || parameter.id}`} />
                    {!reusable && <button className="table-icon-action danger" type="button" aria-label={`Delete profile parameter ${parameter.name || parameter.id}`} onClick={() => onRemoveParameter(parentId, parameter)}><span className="icon" aria-hidden="true">delete</span></button>}
                </div>)}</div>}
            </div>
            <div className="configuration-profile-nested">
                <div className="configuration-subsection-heading">
                    <span className="eyebrow">Nested profiles</span>
                    {reusable ? <span className="configuration-profile-readonly">Reusable profile structure is read-only.</span> : canNest && <div className="nested-profile-add-controls">
                        <div className="configuration-add-controls">
                            <select value={nestedDefinitionChoice} onChange={event => { setNestedDefinitionChoice(event.target.value); setNestedChoice(''); }} aria-label={`Select profile definition to add below ${profile.name || 'profile'}`}>
                                <option value="">Add profile definition…</option>
                                {availableProfiles.map(candidate => <option value={candidate.id} key={candidate.id}>{candidate.name || candidate.id}</option>)}
                            </select>
                            <button className="quiet-button" type="button" onClick={addNestedDefinition} disabled={!nestedDefinitionChoice}>Add</button>
                        </div>
                        {nestedDefinitionChoice && reusableNestedOptions.length > 0 && <div className="configuration-add-controls">
                            <select value={nestedChoice} onChange={event => setNestedChoice(event.target.value)} aria-label={`Select reusable nested profile for ${profile.name || 'profile'}`}>
                                <option value="">Select reusable profile…</option>
                                {reusableNestedOptions.map(nestedItem => <option value={nestedItem.id} key={`${nestedItem.id}-${nestedItem.name}`}>{nestedItem.name || nestedItem.id}</option>)}
                            </select>
                            <button className="quiet-button" type="button" onClick={addNested} disabled={!nestedChoice}>Add</button>
                        </div>}
                    </div>}
                </div>
                {nested.length === 0
                    ? <span className="configuration-profile-empty">{canNest ? 'No nested profiles assigned.' : 'No deeper profiles assigned. New nested profiles are limited to level 3.'}</span>
                    : <div className="configuration-nested-list">{nested.map(child => {
                    const childId = child.id || child.clientId;
                    return <button className="configuration-nested-chip" type="button" key={childId} onClick={() => onOpenNested(childId)} aria-label={`Open ${child.name || child.id}`}>
                    <span>{child.name || child.id}</span>
                    <span className="icon" aria-hidden="true">chevron_right</span>
                </button>;
                })}</div>}
            </div>
        </div>}
    </article>;
}
function ConfigurationProfilesSectionV2({ profiles, availableProfiles, availableNestedProfiles, availableParameters, onAdd, onAddReusable, onRemove, onAddNestedProfileDefinition, onAddNested, onRemoveNested, onAddParameter, onUpdateParameter, onUpdateProfileName, onRemoveParameter, onOpenNested, showLifecycleDetails = false, onUpdateProfileLifecycle }) {
    const [collapsed, setCollapsed] = useState(false);
    const [expandedProfileId, setExpandedProfileId] = useState('');
    const [profileChoice, setProfileChoice] = useState('');
    const [reusableChoice, setReusableChoice] = useState('');
    const reusableOptions = availableNestedProfiles.filter(profile => profile.definitionId === profileChoice && !profiles.some(current => current.id === profile.id));
    const addProfile = () => {
        if (!profileChoice) return;
        setExpandedProfileId(onAdd(profileChoice) || '');
        setProfileChoice('');
        setCollapsed(false);
    };
    const addReusableProfile = () => {
        if (!reusableChoice) return;
        setExpandedProfileId(onAddReusable(reusableChoice) || '');
        setReusableChoice('');
        setCollapsed(false);
    };
    return <section className={`native-configuration-profiles ${collapsed ? 'collapsed' : ''}`}>
        <div className="service-items-heading">
            <div className="configuration-section-title">
                <button className="profile-collapse-button" type="button" aria-expanded={!collapsed} aria-label={`${collapsed ? 'Expand' : 'Collapse'} profiles`} onClick={() => setCollapsed(value => !value)}>
                    <span className="icon" aria-hidden="true">{collapsed ? 'chevron_right' : 'expand_more'}</span>
                </button>
                <div><p className="eyebrow">Profiles</p></div>
            </div>
            <div className="configuration-profile-add-controls">
                <div className="configuration-add-controls">
                    <select value={profileChoice} onChange={event => { setProfileChoice(event.target.value); setReusableChoice(''); }} aria-label="Select profile to add">
                        <option value="">Select profile…</option>
                        {availableProfiles.map(profile => <option value={profile.id} key={profile.id}>{profile.name || profile.id}</option>)}
                    </select>
                    <button className="quiet-button" type="button" onClick={addProfile} disabled={!profileChoice}>Add</button>
                </div>
                {reusableOptions.length > 0 && <div className="configuration-add-controls">
                    <select value={reusableChoice} onChange={event => setReusableChoice(event.target.value)} aria-label="Select reusable profile to add">
                        <option value="">Select reusable profile…</option>
                        {reusableOptions.map(profile => <option value={profile.id} key={profile.id}>{profile.name || profile.id}</option>)}
                    </select>
                    <button className="quiet-button" type="button" onClick={addReusableProfile} disabled={!reusableChoice}>Add</button>
                </div>}
            </div>
        </div>
        {!collapsed && (profiles.length === 0 ? <div className="configuration-empty"><span className="icon" aria-hidden="true">account_tree</span>No profiles are assigned to this configuration version.</div> : <div className="configuration-profile-list">{profiles.map(profile => <ConfigurationProfileNodeV2
            key={`${profile.id || profile.clientId || 'new'}-${profile.definitionId}`}
            profile={profile}
            depth={1}
            availableProfiles={availableProfiles}
            availableNestedProfiles={availableNestedProfiles}
            availableParameters={availableParameters}
            onAddNestedProfileDefinition={onAddNestedProfileDefinition}
            onAddNested={onAddNested}
            onRemoveNested={onRemoveNested}
            onAddParameter={onAddParameter}
            onUpdateParameter={onUpdateParameter}
            onUpdateProfileName={onUpdateProfileName}
            onRemoveParameter={onRemoveParameter}
            onRemove={onRemove}
            onOpenNested={onOpenNested}
            showLifecycleDetails={showLifecycleDetails}
            onUpdateProfileLifecycle={onUpdateProfileLifecycle}
            initiallyExpanded={expandedProfileId === (profile.id || profile.clientId)}
        />)}</div>)}
    </section>;
}

function NativeConfigurationParametersSection({ parameters, availableParameters, parameterToAdd, setParameterToAdd, addParameter, updateParameter, updateParameterDetail, updateParameterLifecycle, removeParameter, heading = 'Standalone parameters', emptyMessage = 'No standalone parameters are assigned to this version.', showValueDetails = false, showLifecycleDetails = false, lifecycleMode = 'Specification' }) {
    const [collapsed, setCollapsed] = useState(true);
    const isOrderItem = lifecycleMode === 'OrderItem';
    const addAndExpand = () => {
        addParameter();
        setCollapsed(false);
    };
    return <section className={`native-configuration-parameters ${collapsed ? 'collapsed' : ''}`}>
        <div className="service-items-heading">
            <div className="configuration-section-title">
                <button className="profile-collapse-button" type="button" aria-expanded={!collapsed} aria-label={`${collapsed ? 'Expand' : 'Collapse'} standalone parameters`} onClick={() => setCollapsed(value => !value)}>
                    <span className="icon" aria-hidden="true">{collapsed ? 'chevron_right' : 'expand_more'}</span>
                </button>
                <div>
                    <p className="eyebrow">{heading}</p>
                </div>
            </div>
            <div className="configuration-add-controls">
                <select value={parameterToAdd} onChange={event => setParameterToAdd(event.target.value)} aria-label="Select parameter to add">
                    <option value="">Select parameter…</option>
                    {availableParameters.filter(parameter => !parameters.some(current => current.id === parameter.id)).map(parameter => <option value={parameter.id} key={`${parameter.id}-${parameter.name}`}>{parameter.name || parameter.id}</option>)}
                </select>
                <button className="quiet-button" type="button" onClick={addAndExpand} disabled={!parameterToAdd}>Add</button>
            </div>
        </div>
        {!collapsed && (parameters.length === 0 ? <div className="configuration-empty"><span className="icon" aria-hidden="true">tune</span>{emptyMessage}</div> : <div className="native-configuration-table-wrap">
            <table className="native-configuration-table">
                <thead><tr>{showValueDetails ? <><th>Label</th><th>Parameter</th><th>Type</th><th>Link</th>{showLifecycleDetails && <th>N/A</th>}<th>Value</th><th>Unit</th><th>Start</th><th>End</th><th>Step size</th><th>Decimals</th><th>Values</th></> : <><th>Parameter</th><th>Type</th>{showLifecycleDetails && <th>N/A</th>}<th>Value</th></>}{!showLifecycleDetails && <th>Mandatory</th>}{showLifecycleDetails && (isOrderItem ? <><th>Fixed</th><th>Mandatory</th></> : <><th>Fixed</th><th>Expose at order</th><th>Mandatory at order</th><th>Mandatory at service</th></>)}<th></th></tr></thead>
                <tbody>{parameters.map(parameter => <tr key={`${parameter.id}-${parameter.valueId || 'new'}`}>
                    {showValueDetails ? <><td><strong>{parameter.label || parameter.name || parameter.id}</strong></td><td>{parameter.name || parameter.id}</td><td>{parameter.type || '—'}</td><td><input className="configuration-detail-checkbox" type="checkbox" checked={parameter.isLinked} disabled aria-label={`Link for ${parameter.name || parameter.id}`} /></td>{showLifecycleDetails && <td><input className="configuration-detail-checkbox" type="checkbox" checked={Boolean(parameter.notApplicable)} onChange={event => updateParameterLifecycle(parameter.id, 'notApplicable', event.target.checked)} aria-label={`N/A for ${parameter.name || parameter.id}`} /></td>}<td><ConfigurationParameterValueEditor parameter={parameter} onChange={value => updateParameter(parameter.id, value)} ariaLabel={`Value for ${parameter.name || parameter.id}`} /></td><td>{parameter.unit || '—'}</td>{['start', 'end', 'step', 'decimals'].map(field => <td key={field}>{String(parameter.type || '').toLowerCase() === 'number' ? <input className="configuration-detail-number" type="number" value={parameter[field] ?? ''} onChange={event => updateParameterDetail(parameter.id, field, event.target.value)} aria-label={`${field} for ${parameter.name || parameter.id}`} /> : '—'}</td>)}<td>{String(parameter.type || '').toLowerCase() === 'discrete' ? <button className="quiet-button parameter-values-button" type="button" disabled={!parameter.options?.length} aria-label={`Values for ${parameter.name || parameter.id}`}>…</button> : '—'}</td></> : <><td><strong>{parameter.name || parameter.id}</strong></td><td>{parameter.type || '—'}</td>{showLifecycleDetails && <td><input className="configuration-detail-checkbox" type="checkbox" checked={Boolean(parameter.notApplicable)} onChange={event => updateParameterLifecycle(parameter.id, 'notApplicable', event.target.checked)} aria-label={`N/A for ${parameter.name || parameter.id}`} /></td>}<td><ConfigurationParameterValueEditor parameter={parameter} onChange={value => updateParameter(parameter.id, value)} ariaLabel={`Value for ${parameter.name || parameter.id}`} /></td></>}
                    {!showLifecycleDetails && <td><span className={parameter.mandatory ? 'parameter-required' : 'parameter-optional'}>{parameter.mandatory ? 'Required' : 'Optional'}</span></td>}
                    {showLifecycleDetails && (isOrderItem ? <><td><input className="configuration-detail-checkbox" type="checkbox" checked={Boolean(parameter.fixed)} onChange={event => updateParameterLifecycle(parameter.id, 'fixed', event.target.checked)} aria-label={`Fixed for ${parameter.name || parameter.id}`} /></td><td><input className="configuration-detail-checkbox" type="checkbox" checked={Boolean(parameter.mandatory)} disabled aria-label={`Mandatory for ${parameter.name || parameter.id}`} /></td></> : <><td><input className="configuration-detail-checkbox" type="checkbox" checked={Boolean(parameter.fixed)} onChange={event => updateParameterLifecycle(parameter.id, 'fixed', event.target.checked)} aria-label={`Fixed for ${parameter.name || parameter.id}`} /></td><td><input className="configuration-detail-checkbox" type="checkbox" checked={Boolean(parameter.exposeAtOrder)} onChange={event => updateParameterLifecycle(parameter.id, 'exposeAtOrder', event.target.checked)} aria-label={`Expose at order for ${parameter.name || parameter.id}`} /></td><td><input className="configuration-detail-checkbox" type="checkbox" checked={Boolean(parameter.mandatoryAtOrder)} onChange={event => updateParameterLifecycle(parameter.id, 'mandatoryAtOrder', event.target.checked)} aria-label={`Mandatory at order for ${parameter.name || parameter.id}`} /></td><td><input className="configuration-detail-checkbox" type="checkbox" checked={Boolean(parameter.mandatoryAtService)} onChange={event => updateParameterLifecycle(parameter.id, 'mandatoryAtService', event.target.checked)} aria-label={`Mandatory at service for ${parameter.name || parameter.id}`} /></td></>)}
                    <td><button className="table-icon-action danger" type="button" aria-label={`Delete ${parameter.name || parameter.id}`} onClick={() => removeParameter(parameter.id)}><span className="icon" aria-hidden="true">delete</span></button></td>
                </tr>)}</tbody>
            </table>
        </div>)}
    </section>;
}

function NativeServiceConfigurationsView({ service, actionState, onSave, onConfirm, fetchConfigurations = fetchServiceConfigurations, heading = 'Service configuration', allowVersionDelete = true, allowVersionAdd = false, showVersionMetadata = true, supportsProfiles = true, allowInitialVersion = false, parameterHeading, parameterEmptyMessage, lifecycleMode = 'Specification' }) {
    const configurationFetcher = useCallback(() => fetchConfigurations(service.id), [fetchConfigurations, service.id]);
    const data = useDataList(configurationFetcher);
    const [versionId, setVersionId] = useState('');
    const [draft, setDraft] = useState(null);
    const [parameterToAdd, setParameterToAdd] = useState('');
    const [profileToAdd, setProfileToAdd] = useState('');
    const [deletedConfigurationIds, setDeletedConfigurationIds] = useState([]);
    const [deletedProfileIds, setDeletedProfileIds] = useState([]);
    const [deletedProfileParameterIds, setDeletedProfileParameterIds] = useState([]);
    const [openProfileId, setOpenProfileId] = useState(null);
    const [validationError, setValidationError] = useState('');
    const [showValueDetails, setShowValueDetails] = useState(false);
    const [showLifecycleDetails, setShowLifecycleDetails] = useState(false);
    const busy = actionState?.status === 'running';
    const configurationData = data.items && !Array.isArray(data.items) ? data.items : EMPTY_SERVICE_CONFIGURATION_DATA;
    const configurations = configurationData.versions;
    const availableParameters = configurationData.availableParameters;
    const availableProfiles = configurationData.availableProfiles;
    const availableNestedProfiles = configurationData.availableNestedProfiles || [];
    const selectedProfilePath = openProfileId && draft ? findConfigurationProfilePath(draft.profiles, openProfileId) : null;
    const selectedProfile = selectedProfilePath ? selectedProfilePath[selectedProfilePath.length - 1] : null;
    const profilesAtLevel = selectedProfilePath && selectedProfilePath.length > 1
        ? selectedProfilePath[selectedProfilePath.length - 2].nestedProfiles || []
        : draft?.profiles || [];
    const openProfile = id => { if (id) setOpenProfileId(id); };
    const returnToPreviousProfile = () => {
        if (!selectedProfilePath || selectedProfilePath.length <= 2) {
            setOpenProfileId(null);
            return;
        }
        const parent = selectedProfilePath[selectedProfilePath.length - 2];
        setOpenProfileId(parent ? parent.id || parent.clientId : null);
    };
    const addInitialVersion = () => {
        setVersionId('');
        setDraft({ id: '', name: 'Initial version', description: '', start: '', end: '', active: true, parameters: [], profiles: [] });
        setParameterToAdd('');
        setProfileToAdd('');
        setDeletedConfigurationIds([]);
        setDeletedProfileIds([]);
        setDeletedProfileParameterIds([]);
        setOpenProfileId(null);
        setValidationError('');
    };
    const addNewVersion = () => {
        setVersionId('__new__');
        setDraft({ id: '', name: 'New version', description: '', start: '', end: '', active: false, parameters: [], profiles: [] });
        setParameterToAdd('');
        setProfileToAdd('');
        setDeletedConfigurationIds([]);
        setDeletedProfileIds([]);
        setDeletedProfileParameterIds([]);
        setOpenProfileId(null);
        setValidationError('');
    };
    useEffect(() => {
        if (configurations.length === 0) {
            setVersionId('');
            setDraft(null);
            return;
        }
        const active = configurations.find(version => version.active) || configurations[0];
        setVersionId(current => current && configurations.some(version => version.id === current) ? current : active.id);
    }, [configurations]);
    useEffect(() => {
        if (versionId === '__new__') return;
        const version = configurations.find(item => item.id === versionId);
        setDraft(version ? { ...version, parameters: version.parameters.map(parameter => ({ ...parameter })), profiles: version.profiles.map(profile => ({ ...profile, parameters: profile.parameters.map(parameter => ({ ...parameter })), nestedProfiles: (profile.nestedProfiles || []).map(nested => ({ ...nested, parameters: nested.parameters || [], nestedProfiles: nested.nestedProfiles || [] })) })) } : null);
        setDeletedConfigurationIds([]);
        setDeletedProfileIds([]);
        setDeletedProfileParameterIds([]);
        setOpenProfileId(null);
    }, [configurations, versionId]);
    const update = (field, value) => setDraft(current => ({ ...current, [field]: value }));
    const updateParameter = (id, value) => setDraft(current => ({ ...current, parameters: current.parameters.map(parameter => parameter.id === id ? { ...parameter, value } : parameter) }));
    const updateParameterDetail = (id, field, value) => setDraft(current => ({ ...current, parameters: current.parameters.map(parameter => parameter.id === id ? { ...parameter, [field]: value } : parameter) }));
    const updateParameterLifecycle = (id, field, value) => setDraft(current => ({ ...current, parameters: current.parameters.map(parameter => parameter.id === id ? { ...parameter, [field]: value, ...(field === 'notApplicable' && value ? { value: '' } : {}) } : parameter) }));
    const addParameter = () => {
        const parameter = availableParameters.find(item => item.id === parameterToAdd);
        if (!parameter || !draft || draft.parameters.some(item => item.id === parameter.id)) return;
        setDraft(current => ({ ...current, parameters: [...current.parameters, { ...parameter, value: '', configurationId: '', valueId: '', mandatory: false }] }));
        setParameterToAdd('');
    };
    const removeParameter = id => {
        const parameter = draft?.parameters.find(item => item.id === id);
        if (parameter?.configurationId) setDeletedConfigurationIds(current => [...new Set([...current, parameter.configurationId])]);
        setDraft(current => ({ ...current, parameters: current.parameters.filter(item => item.id !== id) }));
    };
    const createProfile = (definition, siblings = []) => {
        const duplicates = siblings.filter(profile => profile.definitionId === definition.id).length;
        return ({
        id: '',
        clientId: `new-${Date.now()}-${Math.random().toString(36).slice(2)}`,
        name: duplicates ? `${definition.name} #${duplicates + 1}` : definition.name,
        definitionId: definition.id,
        definitionName: definition.name,
        reusable: false,
        mandatory: false,
        parameters: [],
        nestedProfiles: [],
        });
    };
    const addProfile = profileIdOverride => {
        const definition = availableProfiles.find(item => item.id === (profileIdOverride || profileToAdd));
        if (!definition || !draft) return;
        const newProfile = createProfile(definition, draft.profiles);
        setDraft(current => ({ ...current, profiles: [...current.profiles, newProfile] }));
        setProfileToAdd('');
        return newProfile.clientId;
    };
    const addReusableProfile = profileId => {
        const reusable = availableNestedProfiles.find(item => item.id === profileId);
        if (!reusable || !draft || draft.profiles.some(profile => profile.id === reusable.id)) return;
        const newProfile = {
            id: reusable.id,
            clientId: '',
            name: reusable.name,
            definitionId: reusable.definitionId,
            definitionName: reusable.definitionName,
            reusable: true,
            mandatory: false,
            parameters: [],
            nestedProfiles: [],
        };
        setDraft(current => ({ ...current, profiles: [...current.profiles, newProfile] }));
        return newProfile.id;
    };
    const mapProfileTree = (profiles, updater) => profiles.map(profile => {
        const updated = updater(profile);
        return updated === profile ? { ...profile, nestedProfiles: mapProfileTree(profile.nestedProfiles || [], updater) } : updated;
    });
    const addProfileAtLevel = (parentId, definitionId) => {
        const definition = availableProfiles.find(item => item.id === definitionId);
        if (!definition || !draft) return;
        const parentPath = parentId ? findConfigurationProfilePath(draft.profiles, parentId) : null;
        const parent = parentPath ? parentPath[parentPath.length - 1] : null;
        if (parentId && (!parent || parent.reusable)) return;
        const siblings = parent ? parent.nestedProfiles || [] : draft.profiles;
        const newProfile = createProfile(definition, siblings);
        setDraft(current => parent
            ? { ...current, profiles: mapProfileTree(current.profiles, profile => (profile.id === parentId || profile.clientId === parentId) ? { ...profile, nestedProfiles: [...(profile.nestedProfiles || []), newProfile] } : profile) }
            : { ...current, profiles: [...current.profiles, newProfile] });
        setOpenProfileId(newProfile.clientId);
    };
    const addNestedProfileDefinition = (parentId, definitionId) => {
        const definition = availableProfiles.find(item => item.id === definitionId);
        if (!definition || !draft) return;
        const parentPath = findConfigurationProfilePath(draft.profiles, parentId);
        const parent = parentPath ? parentPath[parentPath.length - 1] : null;
        if (!parent || parent.reusable || parentPath.length >= 3) return;
        const newProfile = createProfile(definition, parent.nestedProfiles || []);
        setDraft(current => ({ ...current, profiles: mapProfileTree(current.profiles, profile => (profile.id === parentId || profile.clientId === parentId) ? { ...profile, nestedProfiles: [...(profile.nestedProfiles || []), newProfile] } : profile) }));
        setOpenProfileId(newProfile.clientId);
    };
    const addNestedProfile = (parentId, nestedId) => {
        const nested = availableNestedProfiles.find(profile => profile.id === nestedId);
        if (!nested || !draft || !nestedId) return;
        setDraft(current => ({ ...current, profiles: mapProfileTree(current.profiles, profile => (profile.id === parentId || profile.clientId === parentId) ? { ...profile, nestedProfiles: profile.nestedProfiles.some(item => item.id === nestedId) ? profile.nestedProfiles : [...profile.nestedProfiles, { ...nested, parameters: [], nestedProfiles: [] }] } : profile) }));
    };
    const removeNestedProfile = (parentId, nestedId) => setDraft(current => ({ ...current, profiles: mapProfileTree(current.profiles, profile => (profile.id === parentId || profile.clientId === parentId) ? { ...profile, nestedProfiles: profile.nestedProfiles.filter(item => (item.id || item.clientId) !== nestedId) } : profile) }));
    const addProfileParameter = (profileId, parameterId) => {
        const parameter = availableParameters.find(item => item.id === parameterId);
        if (!parameter || !draft || !parameterId) return;
        setDraft(current => ({ ...current, profiles: mapProfileTree(current.profiles, profile => (profile.id === profileId || profile.clientId === profileId) ? { ...profile, parameters: profile.parameters.some(item => item.id === parameterId) ? profile.parameters : [...profile.parameters, { ...parameter, value: '', valueId: '', mandatory: false }] } : profile) }));
    };
    const updateProfileParameter = (profileId, parameterId, value) => setDraft(current => ({ ...current, profiles: mapProfileTree(current.profiles, profile => (profile.id === profileId || profile.clientId === profileId) ? { ...profile, parameters: profile.parameters.map(parameter => parameter.id === parameterId ? { ...parameter, value } : parameter) } : profile) }));
    const updateProfileName = (profileId, name) => setDraft(current => ({ ...current, profiles: mapProfileTree(current.profiles, profile => (profile.id === profileId || profile.clientId === profileId) ? { ...profile, name } : profile) }));
    const updateProfileLifecycle = (profileId, field, value) => setDraft(current => ({ ...current, profiles: mapProfileTree(current.profiles, profile => (profile.id === profileId || profile.clientId === profileId) ? { ...profile, [field]: value } : profile) }));
    const removeProfileParameter = (profileId, parameter) => {
        if (parameter.valueId) setDeletedProfileParameterIds(current => [...new Set([...current, parameter.valueId])]);
        setDraft(current => ({ ...current, profiles: mapProfileTree(current.profiles, profile => (profile.id === profileId || profile.clientId === profileId) ? { ...profile, parameters: profile.parameters.filter(item => item.id !== parameter.id) } : profile) }));
    };
    const serializeProfile = profile => {
        const { clientId, parameters, nestedProfiles, ...payload } = profile;
        return {
            ...payload,
            MandatoryAtService: profile.mandatoryAtService ?? profile.mandatory ?? false,
            MandatoryAtServiceOrder: profile.mandatoryAtOrder ?? profile.mandatoryAtServiceOrder ?? false,
            ExposeAtServiceOrder: profile.exposeAtOrder ?? profile.exposeAtServiceOrder ?? false,
            Parameters: (parameters || []).map(parameter => ({
                ParameterId: parameter.id,
                ParameterValueId: parameter.valueId,
                Value: parameter.value,
                Mandatory: parameter.mandatory,
                Label: parameter.label,
                IsLinked: parameter.isLinked,
                UnitId: parameter.unitId,
                Start: parameter.start,
                End: parameter.end,
                StepSize: parameter.step,
                Decimals: parameter.decimals,
                Fixed: parameter.fixed,
                ExposeAtServiceOrder: parameter.exposeAtOrder,
                MandatoryAtServiceOrder: parameter.mandatoryAtOrder,
                MandatoryAtService: parameter.mandatoryAtService,
            })),
            NestedProfileIds: (nestedProfiles || []).map(nested => nested.id).filter(Boolean),
            NestedProfiles: (nestedProfiles || []).map(serializeProfile),
        };
    };
    const removeProfile = (id, definitionId, clientId) => {
        if (id) setDeletedProfileIds(current => [...new Set([...current, id])]);
        setDraft(current => ({ ...current, profiles: current.profiles.filter(profile => clientId ? profile.clientId !== clientId : profile.id !== id || profile.definitionId !== definitionId) }));
    };
    const deleteVersion = async () => {
        if (!draft || !await onConfirm(`Delete configuration "${draft.name || 'this version'}"?`)) return;
        await onSave({ 'DOM ID': service.id, Payload: JSON.stringify({ VersionId: draft.id, DeleteVersion: true }) }, data.reloadAndWait);
    };
    const save = async ({ activateVersion = draft?.active } = {}) => {
        if (!draft) return;
        const emptyMandatoryParameter = findEmptyMandatoryParameter(draft.parameters, draft.profiles);
        if (emptyMandatoryParameter) {
            setValidationError(`Required parameter "${emptyMandatoryParameter}" must have a value before saving.`);
            return;
        }
        setValidationError('');
        const payload = {
            DeleteConfigurationIds: deletedConfigurationIds,
            Parameters: draft.parameters.map(parameter => ({
                ConfigurationId: parameter.configurationId,
                ParameterId: parameter.id,
                ParameterValueId: parameter.valueId,
                Value: parameter.value,
                Mandatory: parameter.mandatory,
            })),
        };
        if (showVersionMetadata) {
            Object.assign(payload, {
                VersionId: draft.id,
                VersionName: draft.name,
                Description: draft.description,
                StartDate: draft.start,
                EndDate: draft.end,
                ActivateVersion: Boolean(activateVersion),
            });
        }
        if (supportsProfiles) {
            Object.assign(payload, {
                DeletedProfileParameterIds: deletedProfileParameterIds,
                DeletedProfileIds: deletedProfileIds,
                Profiles: draft.profiles.map(serializeProfile),
            });
        }
        if (await onSave({
            'DOM ID': service.id,
            Payload: JSON.stringify(payload),
        }, data.reloadAndWait)) {
            setVersionId('');
        }
    };
    return <div className="service-drawer-body native-configuration-view">
        <div className="service-drawer-heading">
            <div><p className="eyebrow">Configurations</p><h2>{heading}</h2></div>
            <div className="configuration-view-actions">
                <button className="quiet-button" type="button" onClick={() => setShowValueDetails(value => !value)}>{showValueDetails ? 'Hide Value Details' : 'Show Value Details'}</button>
                <button className="quiet-button" type="button" onClick={() => setShowLifecycleDetails(value => !value)}>{showLifecycleDetails ? 'Hide Lifecycle Details' : 'Show Lifecycle Details'}</button>
                <button className="primary-button" type="button" onClick={save} disabled={busy || !draft}>
                    <span className={busy ? 'icon spin' : 'icon'} aria-hidden="true">{busy ? 'refresh' : 'save'}</span>
                    {busy ? 'Saving…' : 'Save configuration'}
                </button>
            </div>
        </div>
        {validationError && <div className="configuration-validation-error" role="alert"><span className="icon" aria-hidden="true">error</span>{validationError}</div>}
        {data.status === 'loading' ? <div className="configuration-empty"><span className="icon spin" aria-hidden="true">refresh</span>{showVersionMetadata ? 'Loading configuration versions…' : 'Loading configuration parameters…'}</div>
            : data.status === 'error' ? <div className="configuration-empty error"><span className="icon" aria-hidden="true">error</span>{data.error}<button className="quiet-button" type="button" onClick={data.reload}>Retry</button></div>
                : configurations.length === 0 && !draft ? <div className={allowInitialVersion ? 'configuration-empty initial-version-empty' : 'configuration-empty'}><span className="icon" aria-hidden="true">tune</span><span>{showVersionMetadata ? 'No configuration versions are defined for this service yet.' : 'No configuration parameters are available for this order item.'}</span>{allowInitialVersion && <button className="primary-button" type="button" onClick={addInitialVersion} disabled={busy}>Add initial version</button>}</div>
                    : draft && (selectedProfile && selectedProfilePath
                        ? <NativeConfigurationProfileView
                            profile={selectedProfile}
                            profilePath={selectedProfilePath}
                            profilesAtLevel={profilesAtLevel}
                            availableProfiles={availableProfiles}
                            availableNestedProfiles={availableNestedProfiles}
                            availableParameters={availableParameters}
                            onBack={returnToPreviousProfile}
                            onAddProfileAtLevel={addProfileAtLevel}
                            onAddNestedProfileDefinition={addNestedProfileDefinition}
                            onAddNested={addNestedProfile}
                            onRemoveNested={removeNestedProfile}
                            onAddParameter={addProfileParameter}
                            onUpdateParameter={updateProfileParameter}
                            onUpdateProfileName={updateProfileName}
                            onUpdateProfileLifecycle={updateProfileLifecycle}
                            onRemoveParameter={removeProfileParameter}
                            onOpenNested={openProfile}
                            showLifecycleDetails={showLifecycleDetails}
                        />
                        : <>
                            {showVersionMetadata && <section className="native-configuration-version">
                                <div className="configuration-version-toolbar">
                                    <label>Version<select value={versionId} onChange={event => setVersionId(event.target.value)}>{draft.id === '' && <option value="">Initial version</option>}{configurations.map(version => <option value={version.id} key={version.id}>{version.name || version.id}{version.active ? ' (Active)' : ''}</option>)}</select></label>
                                    {versionId === '__new__'
                                        ? <span className="configuration-version-status">Inactive until Activate</span>
                                        : <button className={draft.active ? 'configuration-version-active active' : 'configuration-version-active'} type="button" onClick={() => save({ activateVersion: true })} disabled={busy || draft.active} title={draft.active ? 'This version is active.' : 'Make this version active.'}>
                                            <span className="icon" aria-hidden="true">check_circle</span>{draft.active ? 'Active' : 'Activate'}
                                        </button>}
                                    <div className="configuration-version-actions">
                                        {allowVersionAdd && <button className="quiet-button" type="button" onClick={addNewVersion} disabled={busy}><span className="icon" aria-hidden="true">add</span>New version</button>}
                                        {allowVersionDelete && <button className="danger-button" type="button" onClick={deleteVersion} disabled={busy || draft.active || !draft.id} title={draft.active ? 'Active versions cannot be deleted.' : !draft.id ? 'Save this version before deleting it.' : 'Delete this configuration version.'}><span className="icon" aria-hidden="true">delete</span>Delete configuration</button>}
                                    </div>
                                </div>
                                <div className="configuration-version-form">
                                    <label>Version name<input value={draft.name} onChange={event => update('name', event.target.value)} /></label>
                                    <label>Description<input value={draft.description} onChange={event => update('description', event.target.value)} /></label>
                                    <label>Start date<input type="datetime-local" value={dateTimeInputValue(draft.start)} onChange={event => update('start', event.target.value)} /></label>
                                    <label>End date<input type="datetime-local" value={dateTimeInputValue(draft.end)} onChange={event => update('end', event.target.value)} /></label>
                                </div>
                            </section>}
                            <NativeConfigurationParametersSection parameters={draft.parameters} availableParameters={availableParameters} parameterToAdd={parameterToAdd} setParameterToAdd={setParameterToAdd} addParameter={addParameter} updateParameter={updateParameter} updateParameterDetail={updateParameterDetail} updateParameterLifecycle={updateParameterLifecycle} removeParameter={removeParameter} heading={parameterHeading} emptyMessage={parameterEmptyMessage} showValueDetails={showValueDetails} showLifecycleDetails={showLifecycleDetails} lifecycleMode={lifecycleMode} />
                            {supportsProfiles && <ConfigurationProfilesSectionV2
                                profiles={draft.profiles}
                                availableProfiles={availableProfiles}
                                availableNestedProfiles={availableNestedProfiles}
                                availableParameters={availableParameters}
                                onAdd={addProfile}
                                onAddReusable={addReusableProfile}
                                onRemove={removeProfile}
                                onAddNestedProfileDefinition={addNestedProfileDefinition}
                                onAddNested={addNestedProfile}
                                onRemoveNested={removeNestedProfile}
                                onAddParameter={addProfileParameter}
                                onUpdateParameter={updateProfileParameter}
                                onUpdateProfileName={updateProfileName}
                                onRemoveParameter={removeProfileParameter}
                                onOpenNested={openProfile}
                            />}
                        </>)}
    </div>;
}

function ServiceConfigurationsView({ service }) {
    return <div className="service-drawer-body"><div className="service-drawer-heading"><div><p className="eyebrow">Configurations</p><h2>Service configuration details</h2></div></div><section className="service-configuration-card"><div className="configuration-toolbar"><button className="quiet-button" type="button" disabled>Show Value Details</button><button className="quiet-button" type="button" disabled>Show Lifecycle Details</button></div><div className="configuration-version-grid"><label>Version<select disabled><option>Current version</option></select></label><button className="quiet-button" type="button" disabled>Copy</button><label>Created at<input value={service.start || '—'} readOnly /></label></div><div className="configuration-version-details"><h3>Version details</h3><label>Version name<input value="Current version" readOnly /></label><label>Description<textarea value={`Configuration for ${service.name || 'this service'}`} readOnly /></label><label>Start date<input value={service.start || '—'} readOnly /></label><label>End date<input value={service.end || '—'} readOnly /></label></div><div className="configuration-empty"><span className="icon" aria-hidden="true">tune</span>Detailed configuration parameters are not exposed by the current Service Inventory GQI source.</div></section></div>;
}

function ServiceOperationDialog({ action, initialScript, busy, onClose, onSubmit }) {
    const [name, setName] = useState(initialScript?.name || '');
    const [description, setDescription] = useState(initialScript?.description || '');
    const submit = async event => {
        event.preventDefault();
        if (!name.trim() || !description.trim()) return;
        await onSubmit({ name: name.trim(), description: description.trim(), originalDescription: initialScript?.description || '' });
    };
    return <div className="confirm-layer order-form-layer"><div className="confirm-backdrop" onClick={onClose} aria-hidden="true" /><form className="order-form-dialog specification-form-dialog" onSubmit={submit} aria-labelledby="service-operation-form-title"><header className="order-form-header"><div><p className="eyebrow">Service operations</p><h2 id="service-operation-form-title">{action === 'Update' ? 'Edit service operation' : 'Add service operation'}</h2></div><button className="icon-button" type="button" aria-label="Close service operation form" onClick={onClose}><span className="icon" aria-hidden="true">close</span></button></header><div className="order-form-grid"><label className="wide">Script name<input autoFocus required value={name} onChange={event => setName(event.target.value)} placeholder="Automation script name" /></label><label className="wide">Description<input required value={description} onChange={event => setDescription(event.target.value)} placeholder="Operation description" /></label></div><footer className="order-form-actions"><button className="quiet-button" type="button" onClick={onClose} disabled={busy}>Cancel</button><button className="primary-button" type="submit" disabled={busy}>{busy ? 'Saving…' : action === 'Update' ? 'Save operation' : 'Add operation'}</button></footer></form></div>;
}

function ServiceOperationsView({ service, scripts, busy, onSubmit, onDelete }) {
    const [editor, setEditor] = useState(null);
    const submit = async values => {
        if (await onSubmit(editor.action, values, scripts.reloadAndWait)) {
            setEditor(null);
        }
    };
    const deleteOperation = async script => {
        return onDelete(script, scripts.reloadAndWait);
    };
    return <div className="service-drawer-body"><div className="service-drawer-heading"><div><p className="eyebrow">Operations</p><h2>Service operations</h2></div><button className="primary-button" type="button" onClick={() => setEditor({ action: 'Add', script: null })} disabled={busy}><span className="icon" aria-hidden="true">add</span>Add</button></div><section className="service-operations-panel"><table><thead><tr><th>Description</th><th>Script name</th><th><span className="sr-only">Actions</span></th></tr></thead><tbody>{scripts.status === 'loading' ? <tr><td colSpan="3">Loading operations…</td></tr> : scripts.status === 'error' ? <tr><td colSpan="3">{scripts.error}</td></tr> : scripts.items.length === 0 ? <tr><td colSpan="3">Nothing to show.</td></tr> : scripts.items.map(script => <tr key={`${script.name}-${script.description}`}><td>{script.description || '—'}</td><td>{script.name || '—'}</td><td><div className="service-actions"><button className="table-icon-action" type="button" aria-label={`Edit operation ${script.description || script.name}`} onClick={() => setEditor({ action: 'Update', script })} disabled={busy}><span className="icon" aria-hidden="true">edit</span></button><button className="table-icon-action danger" type="button" aria-label={`Delete operation ${script.description || script.name}`} onClick={() => deleteOperation(script)} disabled={busy}><span className="icon" aria-hidden="true">delete</span></button></div></td></tr>)}</tbody></table></section>{editor && <ServiceOperationDialog action={editor.action} initialScript={editor.script} busy={busy} onClose={() => setEditor(null)} onSubmit={submit} />}</div>;
}

function TabbedServiceDetailPanel({ service, onClose, onEdit, onDelete, onDuplicate, onDeleteItem, onAddItem, onEditItem, onTransitionService, onSaveConfiguration, onConfirmConfiguration, onManageOperation, onDeleteOperation, onConfirmOperation, onActionNotice, onActionFailure, actionState }) {
    const closeButton = useRef(null);
    const [tab, setTab] = useState('service');
    const [itemEditor, setItemEditor] = useState(null);
    const [scriptItem, setScriptItem] = useState(null);
    const itemData = useServiceItems(service.id);
    const relatedOrders = useRelatedServiceOrders(service.id);
    const scripts = useServiceScripts(service.id);
    const transitions = SERVICE_TRANSITIONS[service.status.trim().toLowerCase()] || [];
    const closeItemScript = useCallback(() => setScriptItem(null), []);
    const reloadItems = itemData.reloadAndWait;
    const finishItemScript = useCallback(async ({ cancelled }) => {
        const label = scriptItem?.label || 'service item';
        setScriptItem(null);
        if (cancelled) return;
        onActionNotice('running', 'Refreshing service items…');
        try {
            await reloadItems();
            onActionNotice('success', `Script for "${label}" completed.`);
        } catch (error) {
            onActionFailure(error?.message || 'The service items could not be refreshed.');
        }
    }, [scriptItem, onActionNotice, onActionFailure, reloadItems]);
    const deleteItem = async item => { await onDeleteItem(item, itemData.reloadAndWait); };
    const submitItem = async values => {
        const success = await (itemEditor?.action === 'Edit' ? onEditItem(values, itemData.reloadAndWait) : onAddItem(values, itemData.reloadAndWait));
        if (success) {
            setItemEditor(null);
        }
    };
    const submitOperation = (action, values, refresh) => onManageOperation(service, action, values, refresh);
    const deleteOperation = async (script, refresh) => {
        if (!await onConfirmOperation(`Delete operation "${script.description || script.name}"?`)) return false;
        return onDeleteOperation(service, script, refresh);
    };
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
    const details = [['Service ID', service.serviceId], ['Service specification', service.specification], ['Service category', service.category], ['Category type', service.categoryType], ['Start time', service.start], ['End time', service.end]];
    const relatedOrdersContent = relatedOrders.status === 'loading' ? <div className="service-related-empty"><span className="icon spin" aria-hidden="true">refresh</span>Loading related orders…</div> : relatedOrders.status === 'error' ? <div className="service-related-empty error"><span className="icon" aria-hidden="true">error</span>{relatedOrders.error}</div> : relatedOrders.items.length === 0 ? <div className="service-related-empty"><span className="icon" aria-hidden="true">assignment</span>No related service orders.</div> : <div className="related-orders-table-wrap"><table className="related-orders-table"><thead><tr><th>Name</th><th>Action</th><th>State</th></tr></thead><tbody>{relatedOrders.items.map(({ order, item }, index) => <tr key={`${order.id}-${item.id || index}`}><td>{order.name || 'Unnamed order'}</td><td>{item.action || '—'}</td><td><span className={`state-badge ${stateTone(item.status)}`}>{item.status || 'Unknown'}</span></td></tr>)}</tbody></table></div>;
    const serviceView = <div className="service-drawer-body"><div className="service-drawer-heading"><div><p className="eyebrow">Service Info</p><h2 id="service-detail-title">{service.name || 'Unnamed service'}</h2></div><span className={`state-badge ${serviceTone(service.status)}`}>{service.status || 'Unknown'}</span></div><div className="service-lifecycle" aria-label="Service lifecycle"><div className="service-lifecycle-steps">{lifecycle.map((state, index) => <span className={index === currentIndex ? 'service-lifecycle-step current' : index < currentIndex ? 'service-lifecycle-step complete' : 'service-lifecycle-step'} key={state}>{state}</span>)}</div><div className="service-lifecycle-actions">{transitions.length === 0 ? <span className="service-lifecycle-action-empty">No actions</span> : transitions.map(transition => <button className="service-lifecycle-action" type="button" key={`${transition.label}-${transition.next}`} onClick={() => onTransitionService(service, transition)} disabled={actionState?.status === 'running'}>{transition.label}</button>)}</div></div><div className="service-detail-layout"><section className="service-info-card"><div className="service-info-card-heading"><h3>Service details</h3><div className="drawer-card-actions"><button className="quiet-button drawer-action-button" type="button" onClick={onEdit}><span className="icon" aria-hidden="true">edit</span>Edit</button><button className="quiet-button drawer-action-button" type="button" onClick={() => onDuplicate(service)} disabled={actionState?.status === 'running'}><span className="icon" aria-hidden="true">content_copy</span>Duplicate</button><button className="danger-button drawer-action-button" type="button" onClick={onDelete}><span className="icon" aria-hidden="true">delete</span>Delete</button></div></div><dl className="detail-grid service-detail-grid">{details.map(([label, value]) => <div key={label}><dt>{label}</dt><dd className={label === 'DOM instance ID' ? 'mono' : ''}>{value || '—'}</dd></div>)}</dl></section><section className="service-info-card related-orders-card"><div className="service-info-card-heading"><h3>Related service orders</h3><span className="count-pill">{relatedOrders.status === 'ready' ? relatedOrders.items.length : '—'}</span></div>{relatedOrdersContent}</section></div><section className="service-items-panel"><div className="service-items-heading"><div><p className="eyebrow">Service configuration</p><h3>Service items</h3></div><button className="primary-button" type="button" onClick={() => setItemEditor({ action: 'Add', item: null })} disabled={actionState?.status === 'running'}><span className="icon" aria-hidden="true">add</span>Add item</button></div>{itemData.status === 'loading' ? <div className="service-related-empty"><span className="icon spin" aria-hidden="true">refresh</span>Loading service items…</div> : itemData.items.length === 0 ? <div className="service-related-empty"><span className="icon" aria-hidden="true">inbox</span>Nothing to show.</div> : <div className="service-items-table-wrap"><table className="service-items-table"><thead><tr><th>Label</th><th>Service item type</th><th>Definition reference</th><th>Implementation state</th><th>Monitoring service state</th><th>Actions</th></tr></thead><tbody>{itemData.items.map(item => <tr key={item.id || item.label}><td>{item.label || '—'}</td><td>{item.type || '—'}</td><td>{item.definition || '—'}</td><td>{item.implementationState || '—'}</td><td>{item.monitoringState || '—'}</td><td><div className="service-actions"><button className="table-icon-action" type="button" aria-label={`Launch script for ${item.label || 'service item'}`} title={item.script ? `Launch ${item.script}` : 'No service item script is configured'} onClick={() => setScriptItem(item)} disabled={!item.script || !item.label || actionState?.status === 'running'}><span className="icon" aria-hidden="true">play_circle</span></button><button className="table-icon-action" type="button" aria-label={`Edit ${item.label || 'service item'}`} onClick={() => setItemEditor({ action: 'Edit', item })} disabled={actionState?.status === 'running'}><span className="icon" aria-hidden="true">edit</span></button><button className="table-icon-action danger" type="button" aria-label={`Delete ${item.label || 'service item'}`} onClick={() => deleteItem(item)} disabled={actionState?.status === 'running'}><span className="icon" aria-hidden="true">delete</span></button></div></td></tr>)}</tbody></table></div>}</section></div>;
    return <><div className="drawer-layer"><div className="drawer-backdrop" onClick={onClose} aria-hidden="true" /><aside className="drawer service-drawer" role="dialog" aria-modal="true" aria-labelledby="service-detail-title"><header className="service-drawer-topbar"><nav aria-label="Service detail sections"><button className={tab === 'service' ? 'service-tab active' : 'service-tab'} type="button" onClick={() => setTab('service')} aria-selected={tab === 'service'}><span className="icon" aria-hidden="true">assignment</span>Service</button><button className={tab === 'configurations' ? 'service-tab active' : 'service-tab'} type="button" onClick={() => setTab('configurations')} aria-selected={tab === 'configurations'}><span className="icon" aria-hidden="true">settings</span>Configurations</button><button className={tab === 'operations' ? 'service-tab active' : 'service-tab'} type="button" onClick={() => setTab('operations')} aria-selected={tab === 'operations'}><span className="icon" aria-hidden="true">bolt</span>Operations</button></nav><button ref={closeButton} className="icon-button" type="button" aria-label="Close service details" onClick={onClose}><span className="icon">close</span></button></header>{tab === 'service' ? serviceView : tab === 'configurations' ? <NativeServiceConfigurationsView service={service} actionState={actionState} onSave={onSaveConfiguration} onConfirm={onConfirmConfiguration} allowInitialVersion allowVersionAdd /> : <ServiceOperationsView service={service} scripts={scripts} busy={actionState?.status === 'running'} onSubmit={submitOperation} onDelete={deleteOperation} />}</aside></div>{itemEditor && <ServiceItemDialog key={`${itemEditor.action}-${itemEditor.item?.label || 'new'}`} busy={actionState?.status === 'running'} parentId={service.id} action={itemEditor.action} initialItem={itemEditor.item} context="Service" onClose={() => setItemEditor(null)} onSubmit={submitItem} />}{scriptItem && <IasDialog script={SERVICE_INVENTORY_SCRIPTS.launchItemScript} parameters={{ 'DOM ID': service.id, 'Item Label': scriptItem.label }} eyebrow={`Service item · ${scriptItem.label}`} onClose={closeItemScript} onFinished={finishItemScript} />}</>;
}

function ServicesPage({ selectedId }) {
    const { status, items: services, error, updatedAt, reload, reloadAndWait } = useDataList(fetchServices);
    const { state: actionState, run: runAction, fail: failAction, notify: notifyAction, dismiss: dismissAction } = useAutomationActions();
    const { confirm, dialog: confirmationDialog } = useConfirmation();
    const [search, setSearch] = useState('');
    const [newServiceOpen, setNewServiceOpen] = useState(false);
    const [editServiceTarget, setEditServiceTarget] = useState(null);
    const [logoEditor, setLogoEditor] = useState(null);
    const [statusFilter, setStatusFilter] = useState('all');
    const [sort, setSort] = useState({ key: 'serviceId', direction: 'desc' });
    const [servicesPage, setServicesPage] = useState(1);
    const [servicesPageSize, setServicesPageSize] = useState(10);
    const loading = status === 'loading';
    const hasData = status === 'ready' || (loading && services.length > 0);
    const activeCount = services.filter(isActiveService).length;
    const retiredCount = services.filter(service => service.status.trim().toLowerCase() === 'retired').length;
    const selectedService = selectedId ? services.find(service => service.id === selectedId) : null;
    const createService = () => setNewServiceOpen(true);
    const submitNewService = async values => {
        if (await runAction(SERVICE_INVENTORY_SCRIPTS.create, values, 'Creating service', reloadAndWait)) {
            setNewServiceOpen(false);
        }
    };
    const submitEditedService = async values => {
        if (await runAction(SERVICE_INVENTORY_SCRIPTS.edit, values, 'Saving service', reloadAndWait)) {
            setEditServiceTarget(null);
        }
    };
    const saveServiceLogo = async category => {
        const service = logoEditor?.service;
        if (!service) return;
        const saved = await runAction(SERVICE_INVENTORY_SCRIPTS.edit, {
            Action: 'Edit',
            'DOM ID': service.id,
            Payload: JSON.stringify({
                ServiceID: service.serviceId,
                Name: service.name,
                Category: category,
                Specification: service.specification,
                Organization: service.organization,
                StartTime: service.start,
                EndTime: service.end,
                IndefiniteRuntime: !service.end,
                LinkedService: service.linkedService,
                LinkService: Boolean(service.linkedService),
                GenerateMonitoringService: Boolean(service.linkedService),
            }),
        }, 'Saving service logo', reloadAndWait);
        if (saved) {
            setLogoEditor(null);
        }
    };
    const editService = service => setEditServiceTarget(service);
    const duplicateService = async service => {
        if (!await runAction(SERVICE_INVENTORY_SCRIPTS.duplicate, { 'DOM ID': service.id, Action: 'Duplicate', Payload: JSON.stringify({}) }, 'Duplicating service', reloadAndWait)) return;
        location.hash = 'services';
    };
    const deleteService = async service => {
        if (!await confirm(`Delete "${service.name || 'this service'}"?`)) return;
        let refreshedServices = [];
        if (await runAction(SERVICE_INVENTORY_SCRIPTS.delete, { 'DOM ID': service.id, 'Skip Confirmation': 'true' }, 'Deleting service', async () => {
            refreshedServices = await reloadAndWait();
            if (refreshedServices.some(candidate => candidate.id === service.id)) {
                throw new Error('The delete script completed, but the service is still returned by GQI.');
            }
        })) {
            if (selectedId === service.id) location.hash = 'services';
        }
    };
    const deleteServiceItem = async (service, item, refresh) => {
        if (!await confirm(`Delete "${item.label || 'this service item'}"?`)) return false;
        return runAction(SERVICE_INVENTORY_SCRIPTS.deleteItem, { 'DOM ID': service.id, 'Service Item Label': item.label, 'Skip Confirmation': 'true' }, 'Deleting service item', async () => {
            const remainingItems = await fetchServiceItems(service.id);
            if (remainingItems.some(candidate => candidate.label === item.label)) {
                throw new Error('The delete script completed, but the service item is still returned by GQI.');
            }
            if (refresh) await refresh();
        });
    };
    const addServiceItem = (values, refresh) => runAction(SERVICE_INVENTORY_SCRIPTS.addItem, values, 'Creating service item', refresh);
    const editServiceItem = (values, refresh) => runAction(SERVICE_INVENTORY_SCRIPTS.editItem, values, 'Saving service item', refresh);
    const transitionService = async (service, transition) => {
        await runAction(SERVICE_INVENTORY_SCRIPTS.transition, { ServiceReference: service.id, PreviousState: transitionStateParameter(service.status), NextState: transition.next }, `${transition.label} service`, reloadAndWait);
    };
    const saveServiceConfiguration = (values, refresh) => runAction(SERVICE_CONFIGURATION_SCRIPTS.save, values, 'Saving service configuration', refresh);
    const manageServiceOperation = (service, action, values, refresh) => runAction(SERVICE_SCRIPT_SCRIPTS.manage, {
        Action: action,
        'Service Id': service.id,
        'Script Name': values.name,
        'Script Description': values.description,
        'Original Script Description': values.originalDescription || values.description,
        Payload: JSON.stringify({}),
    }, `${action === 'Update' ? 'Updating' : action === 'Remove' ? 'Deleting' : 'Adding'} service operation`, refresh);
    const deleteServiceOperation = (service, script, refresh) => manageServiceOperation(service, 'Remove', { name: script.name, description: script.description, originalDescription: script.description }, refresh);
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
            const comparison = values[0].localeCompare(values[1], undefined, { sensitivity: 'base', numeric: sort.key === 'serviceId' });
            return sort.direction === 'asc' ? comparison : -comparison;
        });
    }
    useEffect(() => { setServicesPage(1); }, [search, statusFilter, sort.key, sort.direction]);
    useEffect(() => { setServicesPage(current => Math.min(current, Math.max(1, Math.ceil(filteredServices.length / servicesPageSize)))); }, [filteredServices.length, servicesPageSize]);
    const pagedServices = visibleServices.slice((servicesPage - 1) * servicesPageSize, servicesPage * servicesPageSize);
    const toggleSort = key => setSort(current => current.key === key ? { key, direction: current.direction === 'asc' ? 'desc' : 'asc' } : { key, direction: 'asc' });
    const sortIndicator = key => <span className={`table-sort-icon ${sort.key === key ? sort.direction : 'muted'}`} aria-hidden="true" />;
    const sortButton = (key, label) => <button className="table-sort" type="button" onClick={() => toggleSort(key)}>{label}{sortIndicator(key)}</button>;
    let body;
    if (status === 'error') body = <div className="list-message error" role="alert"><span className="icon" aria-hidden="true">error</span><div className="list-message-text"><strong>Services could not be loaded</strong><span>{error}</span></div><button className="quiet-button" type="button" onClick={reload}>Retry</button></div>;
    else if (!hasData) body = <div className="list-message" role="status"><span className="icon spin" aria-hidden="true">refresh</span>Loading services…</div>;
    else if (services.length === 0) body = <div className="list-message" role="status"><span className="icon" aria-hidden="true">inbox</span>No services in inventory yet.</div>;
    else if (filteredServices.length === 0) body = <div className="services-empty" role="status"><span className="icon" aria-hidden="true">search_off</span><div><strong>No services match these filters</strong><span>Try a different search or status.</span></div><button className="quiet-button" type="button" onClick={() => { setSearch(''); setStatusFilter('all'); }}>Clear filters</button></div>;
    else body = <div className="services-table-wrap" aria-busy={loading}><table className="services-table"><colgroup><col className="service-icon-column" /><col className="service-id-column" /><col className="service-name-column" /><col className="service-status-column" /><col className="service-start-column" /><col className="service-end-column" /><col className="service-specification-column" /><col className="service-category-column" /><col className="service-category-type-column" /><col className="service-action-column" /></colgroup><thead><tr><th scope="col">Icon</th><th scope="col">{sortButton('serviceId', 'ID')}</th><th scope="col">{sortButton('name', 'Name')}</th><th scope="col">{sortButton('status', 'State')}</th><th scope="col">Start</th><th scope="col">End</th><th scope="col">{sortButton('specification', 'Spec')}</th><th scope="col">{sortButton('category', 'Category')}</th><th scope="col">{sortButton('categoryType', 'Type')}</th><th scope="col"><span className="sr-only">Actions</span></th></tr></thead><tbody>{pagedServices.map(service => <tr className={service.id && service.id === selectedId ? 'service-row selected' : 'service-row'} key={service.id || service.serviceId || service.name} onDoubleClick={() => { if (service.id) location.hash = `services/${encodeURIComponent(service.id)}`; }    }><td><button className="service-logo-button" type="button" aria-label={`Set logo for ${service.name || 'unnamed service'}`} onClick={event => { event.stopPropagation(); setLogoEditor({ service }); }}><span className="service-logo">{service.logo ? <img src={service.logo} alt="" /> : <span className="icon" aria-hidden="true">add_photo_alternate</span>}</span></button></td><td title={service.serviceId || undefined}><span className="table-primary">{service.serviceId || '—'}</span></td><td title={service.name || undefined}><span className="table-primary">{service.name || 'Unnamed service'}</span></td><td><span className={`state-badge ${serviceTone(service.status)}`}>{service.status || 'Unknown'}</span></td><td title={service.start || undefined}><span className="table-description">{service.start || '—'}</span></td><td title={service.end || undefined}><span className="table-description">{service.end || '—'}</span></td><td title={service.specification || undefined}><span className="table-primary">{service.specification || '—'}</span></td><td title={service.category || undefined}><span className="table-primary">{service.category || '—'}</span></td><td title={service.categoryType || undefined}><span className="table-primary">{service.categoryType || '—'}</span></td><td><div className="service-actions"><button className="table-row-open" type="button" aria-label={`Open details of ${service.name || 'unnamed service'}`} onClick={event => { event.stopPropagation(); location.hash = `services/${encodeURIComponent(service.id)}`; }}><span className="icon" aria-hidden="true">chevron_right</span></button></div></td></tr>)}</tbody></table></div>;
    return <main className="page-content services-page"><section className="services-hero"><div><p className="eyebrow">{pages.services.eyebrow}</p><h1>Services</h1><p className="description">Explore the services currently defined in the Service Inventory.</p></div><PageHeaderActions updatedAt={updatedAt} loading={loading} onRefresh={reload} primaryAction={<button className="primary-button" type="button" onClick={createService} disabled={actionState?.status === 'running'}><span className="icon" aria-hidden="true">add</span>Add service</button>} /></section><ActionNotice state={actionState} onDismiss={dismissAction} />{confirmationDialog}{newServiceOpen && <NewServiceDialog busy={actionState?.status === 'running'} existingServices={services} onClose={() => setNewServiceOpen(false)} onSubmit={submitNewService} />}{editServiceTarget && <NewServiceDialog key={editServiceTarget.id} busy={actionState?.status === 'running'} existingServices={services} action="Edit" initialService={editServiceTarget} onClose={() => setEditServiceTarget(null)} onSubmit={submitEditedService} />}{logoEditor && <ServiceLogoDialog service={logoEditor.service} busy={actionState?.status === 'running'} onClose={() => setLogoEditor(null)} onSubmit={saveServiceLogo} />}<section className="services-metrics" aria-label="Service inventory metrics"><button className={statusFilter === 'all' ? 'service-metric selected' : 'service-metric'} type="button" onClick={() => setStatusFilter('all')}><span className="overview-metric-top"><span className="overview-metric-icon icon" aria-hidden="true">dm_service</span><span>All services</span></span><strong>{hasData ? services.length : '—'}</strong><small>Inventory records</small></button><button className={statusFilter === 'active' ? 'service-metric selected' : 'service-metric'} type="button" onClick={() => setStatusFilter('active')}><span className="overview-metric-top"><span className="overview-metric-icon icon" aria-hidden="true">check_circle</span><span>Active</span></span><strong>{hasData ? activeCount : '—'}</strong><small>Designed, reserved, or active</small></button><button className={statusFilter === 'retired' ? 'service-metric selected' : 'service-metric'} type="button" onClick={() => setStatusFilter('retired')}><span className="overview-metric-top"><span className="overview-metric-icon icon" aria-hidden="true">pause_circle</span><span>Retired</span></span><strong>{hasData ? retiredCount : '—'}</strong><small>No longer in operation</small></button></section><section className="services-surface" aria-labelledby="services-table-title"><div className="services-toolbar"><div className="orders-toolbar-title"><div><p className="eyebrow">Service inventory</p><h2 id="services-table-title">All services</h2></div>{hasData && <span className="orders-total-copy">{services.length} {services.length === 1 ? 'service' : 'services'} in total</span>}</div><div className="orders-toolbar-controls"><label className="orders-search"><span className="icon" aria-hidden="true">search</span><span className="sr-only">Search services</span><input type="search" value={search} onChange={event => setSearch(event.target.value)} placeholder="Search services" aria-label="Search services" />{search && <button type="button" aria-label="Clear search" onClick={() => setSearch('')}><span className="icon" aria-hidden="true">close</span></button>}</label><label className="orders-filter"><span className="sr-only">Filter services by status</span><select value={statusFilter} onChange={event => setStatusFilter(event.target.value)} aria-label="Filter services by status"><option value="all">All statuses</option><option value="active">Active</option><option value="retired">Retired</option></select><span className="icon" aria-hidden="true">expand_more</span></label></div></div>{body}</section><TablePagination page={servicesPage} pageSize={servicesPageSize} total={filteredServices.length} onPageChange={setServicesPage} onPageSizeChange={size => { setServicesPageSize(size); setServicesPage(1); }} />{selectedService && <TabbedServiceDetailPanel service={selectedService} onClose={() => { location.hash = 'services'; }} onEdit={() => editService(selectedService)} onDelete={() => deleteService(selectedService)} onDuplicate={() => duplicateService(selectedService)} onDeleteItem={item => deleteServiceItem(selectedService, item)} onAddItem={addServiceItem} onEditItem={editServiceItem} onTransitionService={transitionService} onSaveConfiguration={saveServiceConfiguration} onConfirmConfiguration={confirm} onManageOperation={manageServiceOperation} onDeleteOperation={deleteServiceOperation} onConfirmOperation={confirm} onActionNotice={notifyAction} onActionFailure={failAction} actionState={actionState} />}</main>;
}

function TabbedSpecificationDetailPanel({ specification, onClose, onEdit, onDelete, onAddItem, onEditItem, onDeleteItem, onSaveConfiguration, onConfirmConfiguration, actionState }) {
    const closeButton = useRef(null);
    const [tab, setTab] = useState('specification');
    const [itemEditor, setItemEditor] = useState(null);
    const itemData = useServiceItems(specification.id);
    const deleteItem = async item => { await onDeleteItem(item, itemData.reloadAndWait); };
    const submitItem = async values => {
        const success = await (itemEditor?.action === 'Edit' ? onEditItem(values, itemData.reloadAndWait) : onAddItem(values, itemData.reloadAndWait));
        if (success) {
            setItemEditor(null);
        }
    };
    useEffect(() => {
        const previous = document.activeElement;
        closeButton.current?.focus();
        const onKey = event => { if (event.key === 'Escape') onClose(); };
        document.addEventListener('keydown', onKey);
        document.body.classList.add('drawer-open');
        return () => { document.removeEventListener('keydown', onKey); document.body.classList.remove('drawer-open'); previous?.focus?.(); };
    }, [onClose]);
    const details = [['Created at', specification.createdAt], ['Created by', specification.createdBy], ['Last modified', specification.lastModified], ['Last modified by', specification.lastModifiedBy]];
    const itemsContent = itemData.status === 'loading' ? <div className="service-related-empty"><span className="icon spin" aria-hidden="true">refresh</span>Loading service items…</div> : itemData.items.length === 0 ? <div className="service-related-empty"><span className="icon" aria-hidden="true">inbox</span>Nothing to show.</div> : <div className="service-items-table-wrap"><table className="service-items-table"><thead><tr><th>Label</th><th>Service item type</th><th>Definition reference</th><th>Service item script</th><th>Actions</th></tr></thead><tbody>{itemData.items.map(item => <tr key={item.id || item.label}><td>{item.label || '—'}</td><td>{item.type || '—'}</td><td>{item.definition || '—'}</td><td>{item.implementation || '—'}</td><td><div className="service-actions"><button className="table-icon-action" type="button" aria-label={`Edit ${item.label || 'service item'}`} onClick={() => setItemEditor({ action: 'Edit', item })} disabled={actionState?.status === 'running'}><span className="icon" aria-hidden="true">edit</span></button><button className="table-icon-action danger" type="button" aria-label={`Delete ${item.label || 'service item'}`} onClick={() => deleteItem(item)} disabled={actionState?.status === 'running'}><span className="icon" aria-hidden="true">delete</span></button></div></td></tr>)}</tbody></table></div>;
    const specificationView = <div className="service-drawer-body"><div className="service-drawer-heading"><div><p className="eyebrow">Service Specification Info</p><h2 id="specification-detail-title">{specification.name || 'Unnamed specification'}</h2></div><div className="drawer-card-actions"><button className="quiet-button drawer-action-button" type="button" onClick={onEdit}><span className="icon" aria-hidden="true">edit</span>Edit</button><button className="danger-button drawer-action-button" type="button" onClick={onDelete}><span className="icon" aria-hidden="true">delete</span>Delete</button></div></div><section className="specification-info-card"><div className="specification-description"><dt>Description</dt><dd>{specification.description || 'No description'}</dd></div><dl className="specification-meta">{details.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value || '—'}</dd></div>)}</dl></section><section className="service-items-panel specification-items-panel"><div className="service-items-heading"><div><h3>Service Items</h3></div><button className="primary-button" type="button" onClick={() => setItemEditor({ action: 'Add', item: null })} disabled={actionState?.status === 'running'}><span className="icon" aria-hidden="true">add</span>Add</button></div>{itemsContent}</section></div>;
    const configurationView = <NativeServiceConfigurationsView service={specification} actionState={actionState} onSave={onSaveConfiguration} onConfirm={onConfirmConfiguration} fetchConfigurations={fetchServiceSpecificationConfigurations} heading="Specification configuration" allowInitialVersion allowVersionAdd />;
    return <><div className="drawer-layer"><div className="drawer-backdrop" onClick={onClose} aria-hidden="true" /><aside className="drawer service-drawer specification-drawer" role="dialog" aria-modal="true" aria-labelledby="specification-detail-title"><header className="service-drawer-topbar"><nav aria-label="Specification detail sections"><button className={tab === 'specification' ? 'service-tab active' : 'service-tab'} type="button" onClick={() => setTab('specification')} aria-selected={tab === 'specification'}><span className="icon" aria-hidden="true">assignment</span>Service Specification</button><button className={tab === 'configurations' ? 'service-tab active' : 'service-tab'} type="button" onClick={() => setTab('configurations')} aria-selected={tab === 'configurations'}><span className="icon" aria-hidden="true">settings</span>Configurations</button></nav><button ref={closeButton} className="icon-button" type="button" aria-label="Close specification details" onClick={onClose}><span className="icon">close</span></button></header>{tab === 'specification' ? specificationView : configurationView}</aside></div>{itemEditor && <ServiceItemDialog key={`${itemEditor.action}-${itemEditor.item?.label || 'new'}`} busy={actionState?.status === 'running'} parentId={specification.id} action={itemEditor.action} initialItem={itemEditor.item} context="Specification" onClose={() => setItemEditor(null)} onSubmit={submitItem} />}</>;
}

function ServiceCategoryDialog({ busy, onClose, onSubmit, initialCategory = null, action = 'Add', existingCategories = [] }) {
    const [form, setForm] = useState(() => ({
        name: initialCategory?.name || '',
        type: initialCategory?.type || 'Channel',
        icon: initialCategory?.icon || '',
    }));
    const logoData = useDataList(fetchDocumentHubLogos);
    const update = (field, value) => setForm(current => ({ ...current, [field]: value }));
    const duplicateName = Boolean(form.name.trim()) && existingCategories.some(category => category.id !== initialCategory?.id && String(category.name || '').trim().toLowerCase() === form.name.trim().toLowerCase());
    const missingName = !form.name.trim();
    const missingType = !form.type.trim();
    const submit = async event => {
        event.preventDefault();
        if (missingName || missingType || duplicateName) return;
        await onSubmit({
            Action: action,
            domId: initialCategory?.id || '',
            Payload: JSON.stringify({ Name: form.name.trim(), Type: form.type.trim(), Icon: form.icon.trim() }),
        });
    };
    const logos = logoData.items.some(logo => logo.icon === form.icon) || !form.icon ? logoData.items : [{ id: `current-${form.icon}`, name: 'Current logo', icon: form.icon }, ...logoData.items];
    return <div className="confirm-layer order-form-layer"><div className="confirm-backdrop" onClick={onClose} aria-hidden="true" /><form className="order-form-dialog specification-form-dialog" onSubmit={submit} aria-labelledby="service-category-form-title"><header className="order-form-header"><div><p className="eyebrow">Service design</p><h2 id="service-category-form-title">{action === 'Edit' ? 'Edit service category' : 'New service category'}</h2></div><button className="icon-button" type="button" aria-label="Close service category form" onClick={onClose}><span className="icon" aria-hidden="true">close</span></button></header><div className="order-form-grid"><label className="wide">Name<input autoFocus value={form.name} onChange={event => update('name', event.target.value)} placeholder="Category name" aria-invalid={missingName || duplicateName || undefined} />{missingName && <span className="form-field-error">Name is required.</span>}{duplicateName && <span className="form-field-error">Name already exists!</span>}</label><label>Type<input value={form.type} onChange={event => update('type', event.target.value)} placeholder="Channel" aria-invalid={missingType || undefined} />{missingType && <span className="form-field-error">Type is required.</span>}</label><label className="wide">Document Hub logo<select aria-label="Document Hub logo" value={form.icon} onChange={event => update('icon', event.target.value)} disabled={logoData.status === 'loading' && logos.length === 0}><option value="">No logo</option>{logoData.status === 'loading' && logos.length === 0 && <option disabled>Loading Document Hub images…</option>}{logos.map(logo => <option key={logo.id} value={logo.icon}>{logo.name}</option>)}</select>{logoData.status === 'error' && <span className="form-field-error">Document Hub images could not be loaded.</span>}</label>{form.icon && <div className="category-logo-preview wide"><span>Preview</span><img src={form.icon} alt="" onError={event => { event.currentTarget.hidden = true; }} /></div>}</div><footer className="order-form-actions"><button className="quiet-button" type="button" onClick={onClose} disabled={busy}>Cancel</button><button className="primary-button" type="submit" disabled={busy || missingName || missingType || duplicateName}>{busy ? 'Saving…' : action === 'Edit' ? 'Save' : 'Create'}</button></footer></form></div>;
}

function ServiceCategoriesPage() {
    const { status, items: categories, error, updatedAt, reload, reloadAndWait } = useDataList(fetchServiceCategories);
    const { state: actionState, run: runAction, fail: failAction, dismiss: dismissAction } = useAutomationActions();
    const { confirm, dialog: confirmationDialog } = useConfirmation();
    const [editor, setEditor] = useState(null);
    const loading = status === 'loading';
    const hasData = status === 'ready' || (loading && categories.length > 0);
    const submit = async values => {
        if (await runAction(CATALOG_SCRIPTS.manageCategories, values, `${values.Action === 'Edit' ? 'Saving' : 'Creating'} service category`, reloadAndWait)) {
            setEditor(null);
        }
    };
    const deleteCategory = async category => {
        if (!await confirm(`Delete "${category.name || 'this service category'}"?`)) return;
        await runAction(CATALOG_SCRIPTS.manageCategories, { Action: 'Delete', domId: category.id, Payload: JSON.stringify({}) }, 'Deleting service category', async () => {
            const refreshed = await reloadAndWait();
            if (refreshed.some(candidate => candidate.id === category.id)) {
                throw new Error('The category delete script completed, but the category is still returned by GQI.');
            }
        });
    };
    let body;
    if (status === 'error') body = <div className="list-message error" role="alert"><span className="icon" aria-hidden="true">error</span><div className="list-message-text"><strong>Categories could not be loaded</strong><span>{error}</span></div><button className="quiet-button" type="button" onClick={reload}>Retry</button></div>;
    else if (!hasData) body = <div className="list-message" role="status"><span className="icon spin" aria-hidden="true">refresh</span>Loading categories…</div>;
    else if (categories.length === 0) body = <div className="list-message" role="status"><span className="icon" aria-hidden="true">inbox</span>No service categories yet.</div>;
    else body = <div className="catalog-table-wrap"><table className="catalog-table category-table"><colgroup><col className="category-icon-column" /><col className="catalog-name-column" /><col className="category-type-column" /><col className="catalog-open-column" /></colgroup><thead><tr><th scope="col">Icon</th><th scope="col">Name</th><th scope="col">Type</th><th scope="col"><span className="sr-only">Actions</span></th></tr></thead><tbody>{categories.map(category => <tr key={category.id || category.name}><td>{category.icon ? <img className="category-table-icon" src={category.icon} alt="" onError={event => { event.currentTarget.hidden = true; }} /> : <span className="category-icon-placeholder icon" aria-hidden="true">category</span>}</td><td><strong>{category.name || 'Unnamed category'}</strong></td><td>{category.type || '—'}</td><td><div className="service-actions"><button className="table-icon-action" type="button" aria-label={`Edit category ${category.name}`} onClick={() => setEditor({ action: 'Edit', category })} disabled={actionState?.status === 'running'}><span className="icon" aria-hidden="true">edit</span></button><button className="table-icon-action danger" type="button" aria-label={`Delete category ${category.name}`} onClick={() => deleteCategory(category)} disabled={actionState?.status === 'running'}><span className="icon" aria-hidden="true">delete</span></button></div></td></tr>)}</tbody></table></div>;
    return <main className="page-content catalog-page"><section className="services-hero"><div><p className="eyebrow">Service design</p><h1>Service Categories</h1><p className="description">Define the categories, types, and logos used by the service inventory.</p></div><div className="page-header-actions has-primary-action">    <a className="quiet-button" href="#settings">Settings</a><button className="primary-button" type="button" onClick={() => setEditor({ action: 'Add', category: null })} disabled={actionState?.status === 'running'}><span className="icon" aria-hidden="true">add</span>New category</button></div></section><ActionNotice state={actionState} onDismiss={dismissAction} />{confirmationDialog}{editor &&     <ServiceCategoryDialog key={`${editor.action}-${editor.category?.id || 'new'}`} busy={actionState?.status === 'running'} existingCategories={categories} action={editor.action} initialCategory={editor.category} onClose={() => setEditor(null)} onSubmit={submit} />}<section className="services-surface category-surface" aria-labelledby="category-table-title"><div className="services-toolbar"><div className="orders-toolbar-title"><div><p className="eyebrow">Service inventory</p><h2 id="category-table-title">Categories</h2></div>{hasData && <span className="orders-total-copy">{categories.length} categories</span>}</div></div>{body}</section></main>;
}

function CatalogPage({ selectedId }) {
    if (selectedId === 'categories') return <ServiceCategoriesPage />;
    return <SpecificationsCatalogPage selectedId={selectedId} />;
}

function SpecificationsCatalogPage({ selectedId }) {
    const { status, items: specifications, error, updatedAt, reload, reloadAndWait } = useDataList(fetchServiceSpecifications);
    const { state: actionState, run: runAction, fail: failAction, dismiss: dismissAction } = useAutomationActions();
    const { confirm, dialog: confirmationDialog } = useConfirmation();
    const [search, setSearch] = useState('');
    const [newSpecificationOpen, setNewSpecificationOpen] = useState(false);
    const [editSpecificationTarget, setEditSpecificationTarget] = useState(null);
    const [catalogPage, setCatalogPage] = useState(1);
    const [catalogPageSize, setCatalogPageSize] = useState(10);
    const loading = status === 'loading';
    const hasData = status === 'ready' || (loading && specifications.length > 0);
    const totalItems = specifications.reduce((total, specification) => total + specification.itemCount, 0);
    const filteredSpecifications = specifications.filter(specification => specification.name.toLowerCase().includes(search.trim().toLowerCase()));
    useEffect(() => { setCatalogPage(1); }, [search]);
    useEffect(() => { setCatalogPage(current => Math.min(current, Math.max(1, Math.ceil(filteredSpecifications.length / catalogPageSize)))); }, [filteredSpecifications.length, catalogPageSize]);
    const pagedSpecifications = filteredSpecifications.slice((catalogPage - 1) * catalogPageSize, catalogPage * catalogPageSize);
    const selectedSpecification = selectedId ? specifications.find(specification => specification.id === selectedId) : null;
    const deleteSpecification = async specification => {
        if (!await confirm(`Delete "${specification.name || 'this specification'}"?`)) return;
        if (await runAction(CATALOG_SCRIPTS.deleteSpecification, { 'DOM ID': specification.id, 'Skip Confirmation': 'true' }, 'Deleting specification', async () => {
            const refreshedSpecifications = await reloadAndWait();
            if (refreshedSpecifications.some(candidate => candidate.id === specification.id)) {
                throw new Error('The delete script completed, but the specification is still returned by GQI.');
            }
        })) {
            if (selectedId === specification.id) location.hash = 'catalog';
        }
    };
    const createSpecification = () => setNewSpecificationOpen(true);
    const submitNewSpecification = async values => {
        if (await runAction(CATALOG_SCRIPTS.createSpecification, values, 'Creating service specification', reloadAndWait)) {
            setNewSpecificationOpen(false);
        }
    };
    const editSpecification = specification => setEditSpecificationTarget(specification);
    const submitEditedSpecification = async values => {
        if (await runAction(CATALOG_SCRIPTS.createSpecification, values, 'Saving service specification', reloadAndWait)) {
            setEditSpecificationTarget(null);
        }
    };
    const saveSpecificationConfiguration = (values, refresh) => runAction(CATALOG_SCRIPTS.saveConfiguration, values, 'Saving specification configuration', refresh);
    const addSpecificationItem = (values, refresh) => runAction(SERVICE_INVENTORY_SCRIPTS.addItem, values, 'Creating specification item', refresh);
    const editSpecificationItem = (values, refresh) => runAction(SERVICE_INVENTORY_SCRIPTS.editItem, values, 'Saving specification item', refresh);
    const deleteSpecificationItem = async (specification, item, refresh) => {
        if (!await confirm(`Delete "${item.label || 'this specification item'}"?`)) return false;
        return runAction(SERVICE_INVENTORY_SCRIPTS.deleteItem, { 'DOM ID': specification.id, 'Service Item Label': item.label, 'Skip Confirmation': 'true' }, 'Deleting specification item', async () => {
            const remainingItems = await fetchServiceItems(specification.id);
            if (remainingItems.some(candidate => candidate.label === item.label)) {
                throw new Error('The delete script completed, but the specification item is still returned by GQI.');
            }
            if (refresh) await refresh();
        });
    };
    let body;
    if (status === 'error') body = <div className="list-message error" role="alert"><span className="icon" aria-hidden="true">error</span><div className="list-message-text"><strong>Catalog could not be loaded</strong><span>{error}</span></div><button className="quiet-button" type="button" onClick={reload}>Retry</button></div>;
    else if (!hasData) body = <div className="list-message" role="status"><span className="icon spin" aria-hidden="true">refresh</span>Loading catalog…</div>;
    else if (specifications.length === 0) body = <div className="list-message" role="status"><span className="icon" aria-hidden="true">inbox</span>No service specifications yet.</div>;
    else if (filteredSpecifications.length === 0) body = <div className="services-empty" role="status"><span className="icon" aria-hidden="true">search_off</span><div><strong>No specifications match this search</strong><span>Try a different specification name.</span></div><button className="quiet-button" type="button" onClick={() => setSearch('')}>Clear search</button></div>;
    else body = <div className="catalog-table-wrap"><table className="catalog-table"><colgroup><col className="catalog-name-column" /><col className="catalog-created-column" /><col className="catalog-modified-column" /><col className="catalog-open-column" /></colgroup><thead><tr><th scope="col">Specification name</th><th scope="col">Created at</th><th scope="col">Last modified</th><th scope="col"><span className="sr-only">Open</span></th></tr></thead><tbody>{pagedSpecifications.map(specification => <tr className={specification.id === selectedId ? 'catalog-row selected' : 'catalog-row'} key={specification.id || specification.name} onDoubleClick={() => { if (specification.id) location.hash = `catalog/${encodeURIComponent(specification.id)}`; }}><td><strong>{specification.name || 'Unnamed specification'}</strong></td><td>{specification.createdAt || '—'}</td><td>{specification.lastModified || '—'}</td><td><button className="table-row-open" type="button" aria-label={`Open details of ${specification.name || 'unnamed specification'}`} onClick={() => { if (specification.id) location.hash = `catalog/${encodeURIComponent(specification.id)}`; }}><span className="icon" aria-hidden="true">chevron_right</span></button></td></tr>)}</tbody></table></div>;
    return <main className="page-content catalog-page"><section className="services-hero"><div><p className="eyebrow">{pages.catalog.eyebrow}</p><h1>Catalog</h1><p className="description">See the service specifications that shape the inventory.</p></div><PageHeaderActions updatedAt={updatedAt} loading={loading} onRefresh={reload} primaryAction={<button className="primary-button" type="button" onClick={createSpecification} disabled={actionState?.status === 'running'}><span className="icon" aria-hidden="true">add</span>Add specification</button>} /></section><ActionNotice state={actionState} onDismiss={dismissAction} />{confirmationDialog}{newSpecificationOpen && <NewSpecificationDialog busy={actionState?.status === 'running'} existingSpecificationNames={specifications.map(specification => specification.name)} onClose={() => setNewSpecificationOpen(false)} onSubmit={submitNewSpecification} />}{editSpecificationTarget && <NewSpecificationDialog key={editSpecificationTarget.id} busy={actionState?.status === 'running'} existingSpecificationNames={specifications.map(specification => specification.name)} action="Edit" initialSpecification={editSpecificationTarget} onClose={() => setEditSpecificationTarget(null)} onSubmit={submitEditedSpecification} />}<section className="services-metrics" aria-label="Catalog metrics"><div className="service-metric static"><span>Specifications</span><strong>{hasData ? specifications.length : '—'}</strong><small>Reusable service definitions</small></div><div className="service-metric static"><span>Service items</span><strong>{hasData ? totalItems : '—'}</strong><small>Defined across specifications</small></div></section><section className="services-surface" aria-labelledby="catalog-table-title"><div className="services-toolbar"><div className="orders-toolbar-title"><div><p className="eyebrow">Service design</p><h2 id="catalog-table-title">Specifications</h2></div>{hasData && <span className="orders-total-copy">{specifications.length} specifications</span>}</div><div className="orders-toolbar-controls"><label className="orders-search"><span className="icon" aria-hidden="true">search</span><span className="sr-only">Search specifications</span><input type="search" value={search} onChange={event => setSearch(event.target.value)} placeholder="Search specifications" aria-label="Search specifications" />{search && <button type="button" aria-label="Clear search" onClick={() => setSearch('')}><span className="icon" aria-hidden="true">close</span></button>}</label></div></div>{body}</section><TablePagination page={catalogPage} pageSize={catalogPageSize} total={filteredSpecifications.length} onPageChange={setCatalogPage} onPageSizeChange={size => { setCatalogPageSize(size); setCatalogPage(1); }} />{selectedSpecification && <TabbedSpecificationDetailPanel specification={selectedSpecification} onClose={() => { location.hash = 'catalog'; }} onEdit={() => editSpecification(selectedSpecification)} onDelete={() => deleteSpecification(selectedSpecification)} onAddItem={addSpecificationItem} onEditItem={editSpecificationItem} onDeleteItem={(item, refresh) => deleteSpecificationItem(selectedSpecification, item, refresh)} onSaveConfiguration={saveSpecificationConfiguration} onConfirmConfiguration={confirm} actionState={actionState} />}</main>;
}

function useServiceOrders() {
    const [state, setState] = useState({ status: 'loading', orders: [], error: '', updatedAt: null });
    const latestRequest = useRef(0);
    const load = useCallback(({ rejectOnError = false } = {}) => {
        const request = ++latestRequest.current;
        setState(current => ({ ...current, status: 'loading', error: '' }));
        return fetchServiceOrders().then(orders => { if (request === latestRequest.current) setState({ status: 'ready', orders, error: '', updatedAt: new Date() }); return orders; }, error => { if (request === latestRequest.current && !(error instanceof DataMinerAuthError)) setState({ status: 'error', orders: [], error: error?.message || 'Unknown error.', updatedAt: null }); if (rejectOnError) throw error; return []; });
    }, []);
    const reloadAndWait = useCallback(() => load({ rejectOnError: true }), [load]);
    useEffect(() => { load(); return () => { latestRequest.current++; }; }, [load]);
    return { ...state, reload: load, reloadAndWait };
}

function useAutomationActions() {
    const [state, setState] = useState(null);
    useEffect(() => {
        if (!state || state.status === 'running') return undefined;
        const timeout = window.setTimeout(() => setState(null), state.status === 'error' ? 8000 : 4000);
        return () => window.clearTimeout(timeout);
    }, [state]);
    const run = useCallback(async (script, values, label, refresh) => {
        setState({ status: 'running', message: `${label}…` });
        try {
            await executeAutomationScript(script, values);
            if (refresh) await refresh();
            setState({ status: 'success', message: `${label} completed.` });
            return true;
        } catch (error) {
            if (error instanceof DataMinerAuthError) return false;
            setState({ status: 'error', message: error?.message || `${label} failed.` });
            return false;
        }
    }, []);
    return { state, run, fail: message => setState({ status: 'error', message }), notify: (status, message) => setState({ status, message }), dismiss: () => setState(null) };
}

function ActionNotice({ state, onDismiss }) {
    if (!state) return null;
    const icon = state.status === 'error' ? 'error' : state.status === 'success' ? 'check_circle' : 'refresh';
    return <div className={`action-notice ${state.status}`} role={state.status === 'error' ? 'alert' : 'status'}><span className={state.status === 'running' ? 'icon spin' : 'icon'} aria-hidden="true">{icon}</span><span>{state.message}</span><button className="icon-button" type="button" aria-label="Dismiss notification" onClick={onDismiss}><span className="icon" aria-hidden="true">close</span></button></div>;
}

function TablePagination({ page, pageSize, total, onPageChange, onPageSizeChange }) {
    if (total === 0) return null;
    const pageCount = Math.max(1, Math.ceil(total / pageSize));
    return <nav className="table-pagination" aria-label="Table pagination"><span className="table-pagination-summary">{Math.min((page - 1) * pageSize + 1, total)}–{Math.min(page * pageSize, total)} of {total}</span><div className="table-pagination-controls"><label>Rows<select value={pageSize} onChange={event => onPageSizeChange(Number(event.target.value))}><option value="10">10</option><option value="25">25</option><option value="50">50</option><option value="100">100</option></select></label><button className="quiet-button" type="button" onClick={() => onPageChange(page - 1)} disabled={page <= 1}>Previous</button><span>Page {page} of {pageCount}</span><button className="quiet-button" type="button" onClick={() => onPageChange(page + 1)} disabled={page >= pageCount}>Next</button></div></nav>;
}

function ConfirmDialog({ message, onConfirm, onCancel }) {
    const cancelButton = useRef(null);
    useEffect(() => {
        cancelButton.current?.focus();
        const onKey = event => {
            if (event.key === 'Escape') onCancel();
        };
        document.addEventListener('keydown', onKey);
        document.body.classList.add('confirm-open');
        return () => {
            document.removeEventListener('keydown', onKey);
            document.body.classList.remove('confirm-open');
        };
    }, [onCancel]);
    return <div className="confirm-layer"><div className="confirm-backdrop" onClick={onCancel} aria-hidden="true" /><section className="confirm-dialog" role="alertdialog" aria-modal="true" aria-labelledby="confirm-title"><div className="confirm-icon icon" aria-hidden="true">delete</div><div className="confirm-copy"><h2 id="confirm-title">Confirm deletion</h2><p>{message}</p></div><div className="confirm-actions"><button ref={cancelButton} className="quiet-button" type="button" onClick={onCancel}>Cancel</button><button className="danger-button" type="button" onClick={onConfirm}><span className="icon" aria-hidden="true">delete</span>Delete</button></div></section></div>;
}

function useConfirmation() {
    const [request, setRequest] = useState(null);
    const confirm = useCallback(message => new Promise(resolve => setRequest({ message, resolve })), []);
    const close = useCallback(result => {
        request?.resolve(result);
        setRequest(null);
    }, [request]);
    return { confirm, dialog: request ? <ConfirmDialog message={request.message} onConfirm={() => close(true)} onCancel={() => close(false)} /> : null };
}

function NewOrderDialog({ busy, onClose, onSubmit, initialOrder = null, action = 'Add', existingOrderNames = [] }) {
    const [form, setForm] = useState(() => ({ name: initialOrder?.name || '', description: initialOrder?.description || '', externalId: initialOrder?.externalId || '', priority: initialOrder?.priority || 'High', organization: initialOrder?.organization || '', requestedStart: initialOrder?.start || '', requestedEnd: '', completionEnabled: Boolean(initialOrder?.start) }));
    const organizationData = useDataList(fetchServiceOrganizations);
    const [closed, setClosed] = useState(false);
    const [submitAttempted, setSubmitAttempted] = useState(false);
    const update = (field, value) => setForm(current => ({ ...current, [field]: value }));
    const close = () => { setClosed(true); onClose(); };
    const closeFromClick = event => { event.preventDefault(); event.stopPropagation(); close(); };
    const placeholder = initialOrder?.externalId || initialOrder?.id?.split('-')[0]?.toUpperCase() || `ORDER-${String(existingOrderNames.length + 1).padStart(6, '0')}`;
    const duplicateName = Boolean(form.name.trim()) && existingOrderNames.some(name => String(name || '').trim().toLowerCase() === form.name.trim().toLowerCase() && name !== initialOrder?.name);
    const invalidCompletionDates = form.completionEnabled
        && form.requestedStart
        && form.requestedEnd
        && Date.parse(form.requestedEnd) < Date.parse(form.requestedStart);
    const organizationOptions = organizationData.items.some(organization => organization.name === form.organization) || !form.organization
        ? organizationData.items
        : [{ id: `current-${form.organization}`, name: form.organization }, ...organizationData.items];
    const submit = async event => {
        event.preventDefault();
        setSubmitAttempted(true);
        if (duplicateName) return;
        const name = form.name.trim() || placeholder;
        await onSubmit({
            Action: action,
            'DOM ID': initialOrder?.id,
            Payload: JSON.stringify({
                Name: name,
                Description: form.description,
                ExternalId: form.externalId,
                Priority: form.priority,
                Organization: form.organization,
                RequestedStart: form.completionEnabled ? form.requestedStart : '',
                RequestedEnd: form.completionEnabled ? form.requestedEnd : '',
                CompletionSettingsEnabled: form.completionEnabled,
            }),
        });
    };
    if (closed) return null;
    return <div className="confirm-layer order-form-layer"><div className="confirm-backdrop" onClick={closeFromClick} aria-hidden="true" /><form className="order-form-dialog" onSubmit={submit} aria-labelledby="new-order-title"><header className="order-form-header"><div><p className="eyebrow">Service operations</p><h2 id="new-order-title">{action === 'Edit' ? 'Edit service order' : 'New service order'}</h2></div><button className="icon-button" type="button" aria-label="Close service order form" onClick={closeFromClick}><span className="icon" aria-hidden="true">close</span></button></header><div className="order-form-grid"><label className="wide">Label<input autoFocus value={form.name} onChange={event => update('name', event.target.value)} placeholder={placeholder} aria-invalid={duplicateName || undefined} />{duplicateName && <span className="form-field-error">Name already exists!</span>}{submitAttempted && !form.name.trim() && !duplicateName && <span className="form-field-hint">Placeholder will be used: {placeholder}</span>}</label><label className="wide">Description<textarea value={form.description} onChange={event => update('description', event.target.value)} placeholder="Describe the service request" /></label><label>External ID<input value={form.externalId} onChange={event => update('externalId', event.target.value)} placeholder="Optional external ID" /></label><label>Priority<select value={form.priority} onChange={event => update('priority', event.target.value)}><option>High</option><option>Medium</option><option>Low</option></select></label><label className="wide">Organization<select aria-label="Organization" value={form.organization} onChange={event => update('organization', event.target.value)} disabled={organizationData.status === 'loading' && organizationOptions.length === 0}><option value="">No organization</option>{organizationData.status === 'loading' && organizationOptions.length === 0 && <option disabled>Loading organizations…</option>}{organizationOptions.map(organization => <option key={organization.id || organization.name} value={organization.name}>{organization.name}</option>)}</select>{organizationData.status === 'error' && <span className="form-field-error">Organizations could not be loaded. You can leave this empty.</span>}</label><button className="completion-toggle wide" type="button" onClick={() => setForm(current => ({ ...current, completionEnabled: !current.completionEnabled }))}>{form.completionEnabled ? 'Hide Completion Details' : 'Show Completion Details'}</button>{form.completionEnabled && <div className="completion-section wide"><label><span>Completion settings enabled</span><input type="checkbox" checked={form.completionEnabled} onChange={event => update('completionEnabled', event.target.checked)} /></label><label>Requested start date<input type="datetime-local" value={form.requestedStart} onChange={event => update('requestedStart', event.target.value)} /></label><label>Requested completed date<input type="datetime-local" value={form.requestedEnd} onChange={event => update('requestedEnd', event.target.value)} />{invalidCompletionDates && <span className="form-field-error">Requested completion cannot be earlier than requested start.</span>}</label></div>}</div><footer className="order-form-actions"><button className="quiet-button" type="button" onClick={closeFromClick} disabled={busy}>Cancel</button><button className="primary-button" type="submit" disabled={busy || duplicateName || invalidCompletionDates}>{busy ? (action === 'Edit' ? 'Saving…' : 'Creating…') : action === 'Edit' ? 'Save' : 'Create order'}</button></footer></form></div>;
}

function NewSpecificationDialog({ busy, onClose, onSubmit, initialSpecification = null, action = 'Add', existingSpecificationNames = [] }) {
    const [form, setForm] = useState(() => ({ name: initialSpecification?.name || '', description: initialSpecification?.description || '', icon: initialSpecification?.icon || '' }));
    const [submitAttempted, setSubmitAttempted] = useState(false);
    const update = (field, value) => setForm(current => ({ ...current, [field]: value }));
    const placeholder = initialSpecification?.name || `Specification #${String(existingSpecificationNames.length + 1).padStart(5, '0')}`;
    const duplicateName = Boolean(form.name.trim()) && existingSpecificationNames.some(name => String(name || '').trim().toLowerCase() === form.name.trim().toLowerCase() && name !== initialSpecification?.name);
    const submit = async event => {
        event.preventDefault();
        setSubmitAttempted(true);
        if (duplicateName) return;
        await onSubmit({ Action: action, 'DOM ID': initialSpecification?.id, Payload: JSON.stringify({ Name: form.name.trim() || placeholder, Description: form.description, Icon: form.icon }) });
    };
    return <div className="confirm-layer order-form-layer"><div className="confirm-backdrop" onClick={onClose} aria-hidden="true" /><form className="order-form-dialog specification-form-dialog" onSubmit={submit} aria-labelledby="new-specification-title"><header className="order-form-header"><div><p className="eyebrow">Service design</p><h2 id="new-specification-title">{action === 'Edit' ? 'Edit service specification' : 'New service specification'}</h2></div><button className="icon-button" type="button" aria-label="Close service specification form" onClick={onClose}><span className="icon" aria-hidden="true">close</span></button></header><div className="order-form-grid"><label className="wide">Label<input autoFocus value={form.name} onChange={event => update('name', event.target.value)} placeholder={placeholder} aria-invalid={duplicateName || undefined} />{duplicateName && <span className="form-field-error">Name already exists!</span>}{submitAttempted && !form.name.trim() && !duplicateName && <span className="form-field-hint">Placeholder will be used: {placeholder}</span>}</label><label className="wide">Description<textarea value={form.description} onChange={event => update('description', event.target.value)} placeholder="Describe this service specification" /></label><label className="wide">Icon<input value={form.icon} onChange={event => update('icon', event.target.value)} placeholder="Optional icon path or reference" /></label></div><footer className="order-form-actions"><button className="quiet-button" type="button" onClick={onClose} disabled={busy}>Cancel</button><button className="primary-button" type="submit" disabled={busy || duplicateName}>{busy ? 'Saving…' : action === 'Edit' ? 'Save' : 'Create'}</button></footer></form></div>;
}

function getNextServiceId(services) {
    const highestId = services
        .map(service => {
            const parts = String(service.serviceId || '').trim().split('-');
            return parts[parts.length - 1];
        })
        .map(value => Number.parseInt(value, 10))
        .filter(Number.isFinite)
        .reduce((highest, value) => Math.max(highest, value), 0);
    return `SERVICE-${String(highestId + 1).padStart(5, '0')}`;
}

function getDefaultServiceTimes() {
    const start = new Date(Date.now() + 60 * 60 * 1000);
    const end = new Date(start.getTime() + 60 * 60 * 1000);
    return { start: dateTimeInputValue(start), end: dateTimeInputValue(end) };
}

function NewServiceDialog({ busy, onClose, onSubmit, initialService = null, action = 'Add', existingServices = [] }) {
    const [form, setForm] = useState(() => {
        const defaults = getDefaultServiceTimes();
        return {
            serviceId: initialService?.serviceId || getNextServiceId(existingServices),
            name: initialService?.name || '',
            category: initialService?.category || '',
            specification: initialService?.specification || '',
            organization: initialService?.organization || '',
            startTime: dateTimeInputValue(initialService?.start) || defaults.start,
            endTime: initialService ? dateTimeInputValue(initialService.end) : defaults.end,
            indefiniteRuntime: initialService ? !initialService.end : false,
            linkedService: initialService?.linkedService || '',
            linkService: Boolean(initialService?.linkedService),
            generateMonitoringService: false,
        };
    });
    const categoryData = useDataList(fetchServiceCategories);
    const specificationData = useDataList(fetchServiceSpecifications);
    const organizationData = useDataList(fetchServiceOrganizations);
    const linkedServiceData = useDataList(fetchMonitoringServices);
    const existingServiceFetcher = useCallback(() => initialService?.id ? fetchServices(initialService.id) : Promise.resolve([]), [initialService?.id]);
    const existingServiceData = useDataList(existingServiceFetcher);
    const update = (field, value) => setForm(current => ({ ...current, [field]: value }));
    useEffect(() => {
        const organization = existingServiceData.items[0]?.organization;
        if (action === 'Edit' && organization) {
            setForm(current => current.organization ? current : { ...current, organization });
        }
    }, [action, existingServiceData.items]);
    const serviceId = form.serviceId.trim();
    const serviceName = form.name.trim();
    const generatedName = initialService?.serviceId || serviceId || getNextServiceId(existingServices);
    const currentService = service => service.id === initialService?.id;
    const duplicateServiceId = action !== 'Edit' && Boolean(serviceId) && existingServices.some(service => !currentService(service) && String(service.serviceId || '').trim().toLowerCase() === serviceId.toLowerCase());
    const duplicateServiceName = Boolean(serviceName) && existingServices.some(service => !currentService(service) && String(service.name || '').trim().toLowerCase() === serviceName.toLowerCase());
    const missingServiceId = !serviceId;
    const missingStartTime = !form.startTime;
    const startTime = Date.parse(form.startTime);
    const startInPast = action !== 'Edit' && Number.isFinite(startTime) && startTime < Date.now();
    const missingEndTime = !form.indefiniteRuntime && !form.endTime;
    const endBeforeStart = !form.indefiniteRuntime && isEndBeforeStart(form.startTime, form.endTime);
    const missingLinkedService = form.linkService && !form.linkedService;
    const invalidForm = missingServiceId || duplicateServiceId || duplicateServiceName || missingStartTime || startInPast || missingEndTime || endBeforeStart || missingLinkedService;
    const submit = async event => {
        event.preventDefault();
        if (invalidForm) return;
        await onSubmit({ Action: action, 'DOM ID': initialService?.id, Payload: JSON.stringify({ ServiceID: serviceId, Name: serviceName || generatedName, Category: form.category, Specification: form.specification, Organization: form.organization, StartTime: form.startTime, EndTime: form.endTime, IndefiniteRuntime: form.indefiniteRuntime, LinkedService: form.linkedService, LinkService: form.linkService, GenerateMonitoringService: form.generateMonitoringService }) });
    };
    const categories = categoryData.items.some(category => category.name === form.category) || !form.category ? categoryData.items : [{ id: `current-${form.category}`, name: form.category }, ...categoryData.items];
    const specifications = specificationData.items.some(specification => specification.name === form.specification) || !form.specification ? specificationData.items : [{ id: `current-${form.specification}`, name: form.specification }, ...specificationData.items];
    const organizations = organizationData.items.some(organization => organization.name === form.organization) || !form.organization ? organizationData.items : [{ id: `current-${form.organization}`, name: form.organization }, ...organizationData.items];
    const linkedServices = linkedServiceData.items.some(service => service.id === form.linkedService) || !form.linkedService ? linkedServiceData.items : [{ id: form.linkedService, name: form.linkedService }, ...linkedServiceData.items];
    return <div className="confirm-layer order-form-layer"><div className="confirm-backdrop" onClick={onClose} aria-hidden="true" /><form className="order-form-dialog specification-form-dialog" onSubmit={submit} aria-labelledby="new-service-title"><header className="order-form-header"><div><p className="eyebrow">Service inventory</p><h2 id="new-service-title">{action === 'Edit' ? 'Edit service' : 'New service'}</h2></div><button className="icon-button" type="button" aria-label="Close service form" onClick={onClose}><span className="icon" aria-hidden="true">close</span></button></header><div className="order-form-grid"><label className="wide">Service ID<input autoFocus={action !== 'Edit'} value={form.serviceId} onChange={event => update('serviceId', event.target.value)} placeholder={getNextServiceId(existingServices)} disabled={action === 'Edit'} aria-invalid={missingServiceId || duplicateServiceId || undefined} />{missingServiceId && <span className="form-field-error">Service ID is required.</span>}{duplicateServiceId && <span className="form-field-error">Service ID already exists!</span>}</label><label className="wide">Name<input autoFocus={action === 'Edit'} value={form.name} onChange={event => update('name', event.target.value)} placeholder={generatedName} aria-invalid={duplicateServiceName || undefined} />{duplicateServiceName && <span className="form-field-error">Name already exists!</span>}{!serviceName.trim() && <span className="form-field-hint">Placeholder will be used: {generatedName}</span>}</label><label>Category<select aria-label="Category" value={form.category} onChange={event => update('category', event.target.value)} disabled={categoryData.status === 'loading' && categories.length === 0}><option value="">No category</option>{categoryData.status === 'loading' && categories.length === 0 && <option disabled>Loading categories…</option>}{categories.map(category => <option key={category.id || category.name} value={category.name}>{category.name}</option>)}</select>{categoryData.status === 'error' && <span className="form-field-error">Categories could not be loaded.</span>}</label><label>Service specification<select aria-label="Service specification" value={form.specification} onChange={event => update('specification', event.target.value)} disabled={specificationData.status === 'loading' && specifications.length === 0}><option value="">No specification</option>{specificationData.status === 'loading' && specifications.length === 0 && <option disabled>Loading specifications…</option>}{specifications.map(specification => <option key={specification.id || specification.name} value={specification.name}>{specification.name}</option>)}</select>{specificationData.status === 'error' && <span className="form-field-error">Specifications could not be loaded.</span>}</label><label>Organization<select aria-label="Organization" value={form.organization} onChange={event => update('organization', event.target.value)} disabled={organizationData.status === 'loading' && organizations.length === 0}><option value="">No organization</option>{organizationData.status === 'loading' && organizations.length === 0 && <option disabled>Loading organizations…</option>}{organizations.map(organization => <option key={organization.id || organization.name} value={organization.name}>{organization.name}</option>)}</select>{organizationData.status === 'error' && <span className="form-field-error">Organizations could not be loaded. You can leave this empty.</span>}</label><label>Start time<input type="datetime-local" value={form.startTime} onChange={event => update('startTime', event.target.value)} aria-invalid={missingStartTime || startInPast || undefined} />{missingStartTime && <span className="form-field-error">Start time is required.</span>}{startInPast && <span className="form-field-error">Please make a selection which doesn't lie in the past.</span>}</label><label>End time<input type="datetime-local" value={form.endTime} onChange={event => update('endTime', event.target.value)} disabled={form.indefiniteRuntime} aria-invalid={missingEndTime || endBeforeStart || undefined} />{missingEndTime && <span className="form-field-error">End time is required unless the runtime is indefinite.</span>}{endBeforeStart && <span className="form-field-error">End Time must come after the start time.</span>}</label><label className="form-checkbox"><input type="checkbox" checked={form.indefiniteRuntime} onChange={event => update('indefiniteRuntime', event.target.checked)} />Indefinite (no end time)</label><label>Linked service<select aria-label="Linked service" value={form.linkedService} onChange={event => update('linkedService', event.target.value)} disabled={!form.linkService || (linkedServiceData.status === 'loading' && linkedServices.length === 0)} aria-invalid={missingLinkedService || undefined}><option value="">No linked service</option>{linkedServiceData.status === 'loading' && linkedServices.length === 0 && <option disabled>Loading services…</option>}{linkedServices.map(service => <option key={service.id || service.name} value={service.id}>{service.name}</option>)}</select>{missingLinkedService && <span className="form-field-error">Selection is mandatory!</span>}{linkedServiceData.status === 'error' && <span className="form-field-error">Linked services could not be loaded.</span>}</label><label className="form-checkbox"><input type="checkbox" checked={form.linkService} onChange={event => update('linkService', event.target.checked)} />Link service</label><label className="form-checkbox"><input type="checkbox" checked={form.generateMonitoringService} onChange={event => update('generateMonitoringService', event.target.checked)} />Generate DataMiner monitoring service</label></div><footer className="order-form-actions"><button className="quiet-button" type="button" onClick={onClose} disabled={busy}>Cancel</button><button className="primary-button" type="submit" disabled={busy || invalidForm}>{busy ? (action === 'Edit' ? 'Saving…' : 'Creating…') : action === 'Edit' ? 'Save' : 'Create'}</button></footer></form></div>;
}

function ServiceLogoDialog({ service, busy, onClose, onSubmit }) {
    const categoryData = useDataList(fetchServiceCategories);
    const [category, setCategory] = useState(service.category || '');
    const categories = categoryData.items.some(item => item.name === category) || !category
        ? categoryData.items
        : [{ id: `current-${category}`, name: category, icon: service.logo || '' }, ...categoryData.items];
    const selectedCategory = categories.find(item => item.name === category);
    const submit = async event => {
        event.preventDefault();
        await onSubmit(category);
    };
    return <div className="confirm-layer order-form-layer"><div className="confirm-backdrop" onClick={onClose} aria-hidden="true" /><form className="order-form-dialog specification-form-dialog service-logo-dialog" onSubmit={submit} aria-labelledby="service-logo-title"><header className="order-form-header"><div><p className="eyebrow">Service inventory</p><h2 id="service-logo-title">Assign service logo</h2></div><button className="icon-button" type="button" aria-label="Close service logo form" onClick={onClose}><span className="icon" aria-hidden="true">close</span></button></header><div className="order-form-grid"><label className="wide">Category<select aria-label="Logo category" value={category} onChange={event => setCategory(event.target.value)} disabled={categoryData.status === 'loading' && categories.length === 0}><option value="">No category</option>{categoryData.status === 'loading' && categories.length === 0 && <option disabled>Loading categories…</option>}{categories.map(item => <option key={item.id || item.name} value={item.name}>{item.name}</option>)}</select></label>{selectedCategory?.icon && <div className="category-logo-preview wide"><span>Logo preview</span><img src={selectedCategory.icon} alt="" onError={event => { event.currentTarget.hidden = true; }} /></div>}{categoryData.status === 'error' && <span className="form-field-error wide">Categories could not be loaded.</span>}</div><footer className="order-form-actions"><button className="quiet-button" type="button" onClick={onClose} disabled={busy}>Cancel</button><button className="primary-button" type="submit" disabled={busy || categoryData.status === 'loading'}>{busy ? 'Saving…' : 'Apply logo'}</button></footer></form></div>;
}

function dateTimeInputValue(value) {
    if (!value) return '';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '';
    const pad = number => String(number).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function isEndBeforeStart(startValue, endValue) {
    const startTime = Date.parse(startValue);
    const endTime = Date.parse(endValue);
    return Number.isFinite(startTime) && Number.isFinite(endTime) && endTime < startTime;
}

function ServiceOrderItemDialog({ busy, onClose, onSubmit, orderId, initialItem = null, action = 'Add' }) {
    const categoryData = useDataList(fetchServiceCategories);
    const specificationData = useDataList(fetchServiceSpecifications);
    const serviceData = useDataList(fetchServices);
    const existingItemsFetcher = useCallback(() => orderId ? fetchServiceOrderItems(orderId) : Promise.resolve([]), [orderId]);
    const existingItemsData = useDataList(existingItemsFetcher);
    const [form, setForm] = useState(() => ({
        name: initialItem?.name || '',
        description: initialItem?.description || '',
        itemAction: initialItem?.action || 'Add',
        categoryId: initialItem?.categoryId || '',
        specificationId: initialItem?.specificationId || '',
        serviceId: initialItem?.serviceId || '',
        startTime: dateTimeInputValue(initialItem?.start),
        endTime: dateTimeInputValue(initialItem?.end),
        indefiniteRuntime: !initialItem?.end,
    }));
    const [submitAttempted, setSubmitAttempted] = useState(false);
    const update = (field, value) => setForm(current => ({ ...current, [field]: value }));
    const existingLabels = existingItemsData.items.map(item => item.name).filter(Boolean);
    const placeholder = initialItem?.name || `Service Order Item #${String(existingItemsData.items.length + 1).padStart(3, '0')}`;
    const duplicateLabel = Boolean(form.name.trim()) && existingLabels.some(label => label.trim().toLowerCase() === form.name.trim().toLowerCase() && label !== initialItem?.name);
    const timeApplicable = form.itemAction === 'Add';
    const specificationRequired = form.itemAction === 'Add' && !form.specificationId;
    const serviceRequired = ['Modify', 'Delete'].includes(form.itemAction) && !form.serviceId;
    const endBeforeStart = timeApplicable && !form.indefiniteRuntime && isEndBeforeStart(form.startTime, form.endTime);
    const updateAction = value => { setSubmitAttempted(true); update('itemAction', value); };
    const submit = async event => {
        event.preventDefault();
        setSubmitAttempted(true);
        if (duplicateLabel || specificationRequired || serviceRequired || endBeforeStart) return;
        const name = form.name.trim() || placeholder;
        await onSubmit({
            'DOM ID': orderId,
            'Service Order Item ID': initialItem?.id,
            Action: action,
            Payload: JSON.stringify({
                Name: name,
                Description: form.description,
                ItemAction: form.itemAction,
                StartTime: timeApplicable ? form.startTime : '',
                EndTime: timeApplicable && !form.indefiniteRuntime ? form.endTime : '',
                IndefiniteRuntime: timeApplicable ? form.indefiniteRuntime : true,
                CategoryId: form.categoryId,
                SpecificationId: form.specificationId,
                ServiceId: form.serviceId,
            }),
        });
    };
    const optionError = [categoryData, specificationData, serviceData].find(data => data.status === 'error')?.error;
    return (
        <div className="confirm-layer order-form-layer">
            <div className="confirm-backdrop" onClick={onClose} aria-hidden="true" />
            <form className="order-form-dialog specification-form-dialog" onSubmit={submit} aria-labelledby="service-order-item-form-title">
                <header className="order-form-header">
                    <div>
                        <p className="eyebrow">Service operations</p>
                        <h2 id="service-order-item-form-title">{action === 'Edit' ? 'Edit service order item' : 'Add service order item'}</h2>
                    </div>
                    <button className="icon-button" type="button" aria-label="Close service order item form" onClick={onClose}>
                        <span className="icon" aria-hidden="true">close</span>
                    </button>
                </header>
                <div className="order-form-grid">
                    <label className="wide">
                        Label
                        <input autoFocus value={form.name} onChange={event => update('name', event.target.value)} placeholder={placeholder} aria-invalid={duplicateLabel || undefined} />
                        {duplicateLabel && <span className="form-field-error">Label already exists!</span>}
                        {submitAttempted && !form.name.trim() && !duplicateLabel && <span className="form-field-hint">Placeholder will be used: {placeholder}</span>}
                    </label>
                    <label className="wide">
                        Description
                        <textarea value={form.description} onChange={event => update('description', event.target.value)} placeholder="Describe the requested item" />
                    </label>
                    <label>
                        Action
                        <select value={form.itemAction} onChange={event => updateAction(event.target.value)}>
                            <option value="Add">Add</option>
                            <option value="Delete">Delete</option>
                            <option value="Modify">Modify</option>
                            <option value="NoChange">No Change</option>
                            <option value="Undefined">Undefined</option>
                        </select>
                    </label>
                    <label>
                        Category
                        <select value={form.categoryId} onChange={event => update('categoryId', event.target.value)}>
                            <option value="">None</option>
                            {categoryData.items.map(category => <option value={category.id} key={category.id}>{category.name || category.id}</option>)}
                        </select>
                    </label>
                    <label>
                        Service specification
                        <select value={form.specificationId} onChange={event => update('specificationId', event.target.value)} disabled={form.itemAction === 'Modify' || form.itemAction === 'Delete'} aria-invalid={submitAttempted && specificationRequired || undefined}>
                            <option value="">None</option>
                            {specificationData.items.map(specification => <option value={specification.id} key={specification.id}>{specification.name || specification.id}</option>)}
                        </select>
                        {specificationRequired && <span className="form-field-error">Selection is mandatory!</span>}
                    </label>
                    <label>
                        Service reference
                        <select value={form.serviceId} onChange={event => update('serviceId', event.target.value)} disabled={form.itemAction === 'Add'} aria-invalid={submitAttempted && serviceRequired || undefined}>
                            <option value="">None</option>
                            {serviceData.items.map(service => <option value={service.id} key={service.id}>{service.name || service.serviceId || service.id}</option>)}
                        </select>
                        {serviceRequired && <span className="form-field-error">Selection is mandatory!</span>}
                    </label>
                    {timeApplicable && (
                        <>
                            <label>
                                Start time
                                <input type="datetime-local" value={form.startTime} onChange={event => update('startTime', event.target.value)} />
                            </label>
                            <label>
                                End time
                                <input type="datetime-local" value={form.endTime} onChange={event => update('endTime', event.target.value)} disabled={form.indefiniteRuntime} aria-invalid={endBeforeStart || undefined} />
                                {endBeforeStart && <span className="form-field-error">End Time must come after the start time.</span>}
                            </label>
                            <label className="form-checkbox">
                                <input type="checkbox" checked={form.indefiniteRuntime} onChange={event => update('indefiniteRuntime', event.target.checked)} />
                                Indefinite runtime
                            </label>
                        </>
                    )}
                    {optionError && <p className="form-error wide">{optionError}</p>}
                </div>
                <footer className="order-form-actions">
                    <button className="quiet-button" type="button" onClick={onClose} disabled={busy}>Cancel</button>
                    <button className="primary-button" type="submit" disabled={busy || duplicateLabel || specificationRequired || serviceRequired || endBeforeStart}>
                        {busy ? 'Saving…' : action === 'Edit' ? 'Save' : 'Add item'}
                    </button>
                </footer>
            </form>
        </div>
    );
}

function ServiceItemDialog({ busy, onClose, onSubmit, parentId, initialItem = null, action = 'Add', context = 'Service' }) {
    const existingItemsFetcher = useCallback(() => parentId ? fetchServiceItems(parentId) : Promise.resolve([]), [parentId]);
    const existingItemsData = useDataList(existingItemsFetcher);
    const [form, setForm] = useState(() => ({
        label: initialItem?.label || '',
        type: (initialItem?.type || 'Service').replace(/\s+/g, ''),
        definitionReference: initialItem?.definition || '',
        implementationReference: initialItem?.implementationReference || '',
        script: initialItem?.script || '',
    }));
    const update = (field, value) => setForm(current => ({ ...current, [field]: value }));
    const placeholder = initialItem?.label || `Service Item #${String(existingItemsData.items.length + 1).padStart(3, '0')}`;
    const duplicateLabel = Boolean(form.label.trim()) && existingItemsData.items.some(item => item.label?.trim().toLowerCase() === form.label.trim().toLowerCase() && item.label !== initialItem?.label);
    const submit = async event => {
        event.preventDefault();
        if (duplicateLabel) return;
        await onSubmit({
            'DOM ID': parentId,
            'Service Item Label': initialItem?.label,
            Action: action,
            Payload: JSON.stringify({
                Label: form.label.trim() || placeholder,
                ExistingLabel: initialItem?.label || '',
                Type: form.type,
                DefinitionReference: form.definitionReference,
                ImplementationReference: form.implementationReference,
                Script: form.script,
            }),
        });
    };
    return <div className="confirm-layer order-form-layer"><div className="confirm-backdrop" onClick={onClose} aria-hidden="true" /><form className="order-form-dialog specification-form-dialog" onSubmit={submit} aria-labelledby="service-item-form-title"><header className="order-form-header"><div><p className="eyebrow">{context === 'Specification' ? 'Service design' : 'Service inventory'}</p><h2 id="service-item-form-title">{action === 'Edit' ? 'Edit service item' : 'Add service item'}</h2></div><button className="icon-button" type="button" aria-label="Close service item form" onClick={onClose}><span className="icon" aria-hidden="true">close</span></button></header><div className="order-form-grid"><label className="wide">Label<input autoFocus value={form.label} onChange={event => update('label', event.target.value)} placeholder={placeholder} aria-invalid={duplicateLabel || undefined} />{duplicateLabel && <span className="form-field-error">Label already exists!</span>}{!form.label.trim() && <span className="form-field-hint">Placeholder will be used: {placeholder}</span>}</label><label>Type<select value={form.type} onChange={event => update('type', event.target.value)}><option value="Service">Service</option><option value="Workflow">Workflow</option><option value="SRMBooking">SRM booking</option></select></label><label>Definition reference<input value={form.definitionReference} onChange={event => update('definitionReference', event.target.value)} placeholder="Definition reference" /></label><label>Implementation reference<input value={form.implementationReference} onChange={event => update('implementationReference', event.target.value)} placeholder="Optional reference ID" /></label><label className="wide">Service item script<input value={form.script} onChange={event => update('script', event.target.value)} placeholder="Optional script name" /></label></div><footer className="order-form-actions"><button className="quiet-button" type="button" onClick={onClose} disabled={busy}>Cancel</button><button className="primary-button" type="submit" disabled={busy || duplicateLabel}>{busy ? 'Saving…' : action === 'Edit' ? 'Save' : 'Add item'}</button></footer></form></div>;
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

function OrderItems({ orderId, refreshKey, onAdd, onEdit, onDelete, onSelect, onInitialize, initializingItemId, configurationBlockedItemId, busy }) {
    const { status, items, error, retry } = useOrderItems(orderId, refreshKey);
    let body;
    if (status === 'error') body = <div className="list-message error" role="alert"><span className="icon" aria-hidden="true">error</span><div className="list-message-text"><strong>Order items could not be loaded</strong><span>{error}</span></div><button className="quiet-button" type="button" onClick={retry}>Retry</button></div>;
    else if (status === 'loading') body = <div className="list-message" role="status"><span className="icon spin" aria-hidden="true">refresh</span>Loading order items…</div>;
    else if (items.length === 0) body = <div className="list-message" role="status"><span className="icon" aria-hidden="true">inbox</span>This order has no items.</div>;
    else body = <ol className="item-list">{items.map((item, index) => {
        const action = String(item.action || '').trim().toLowerCase();
        const initializing = initializingItemId === item.id;
        const serviceLinked = Boolean(item.serviceId);
        const configurationBlocked = configurationBlockedItemId === item.id;
        const canInitialize = Boolean(item.id && item.specificationId && !item.serviceId && action !== 'delete' && !configurationBlocked);
        const initializeTitle = serviceLinked
            ? 'Open the service linked to this order item.'
            : configurationBlocked
                ? 'Configure the service specification before initializing a service.'
            : !item.specificationId
                ? 'Assign a service specification before initializing a service.'
                : action === 'delete'
                    ? 'A service cannot be initialized for a delete order item.'
                    : undefined;
        return <li className="order-item selectable" key={`${item.id || item.name || 'item'}-${index}`} tabIndex={item.id ? 0 : -1} role={item.id ? 'button' : undefined} onClick={() => onSelect(item)} onKeyDown={event => { if (item.id && (event.key === 'Enter' || event.key === ' ') ) { event.preventDefault(); onSelect(item); } }}>
            <div className="order-item-head">
                <div className="order-item-title"><span className="order-item-name">{item.name || 'Unnamed item'}</span></div>
                <span className={`state-badge ${stateTone(item.status)}`}>{item.status || 'Unknown'}</span>
            </div>
            <dl className="item-facts">{[['Action', item.action], ['Category', item.category], ['Specification', item.specification], ['Service', item.service], ['Start', item.start], ['End', item.end]].map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value || '—'}</dd></div>)}</dl>
            {item.description && <p className="order-item-description">{item.description}</p>}
            {configurationBlocked && <p className="order-item-warning" role="note"><span className="icon" aria-hidden="true">info</span>Configure the service specification before initializing a service.</p>}
            <div className="order-item-actions">
                <button className="item-action-button" type="button" onClick={event => { event.stopPropagation(); onEdit(item); }} disabled={busy}><span className="icon" aria-hidden="true">edit</span>Edit</button>
                <button className="item-action-button danger" type="button" onClick={event => { event.stopPropagation(); onDelete(item); }} disabled={busy}><span className="icon" aria-hidden="true">delete</span>Delete</button>
                {(action !== 'delete' || serviceLinked) && <button
                    className={`initialize-button${serviceLinked ? ' initialized' : ''}`}
                    type="button"
                    aria-label={serviceLinked ? `Open service ${item.service || 'linked to this order item'}` : `Initialize service for ${item.name || 'order item'}`}
                    title={initializeTitle}
                    disabled={busy || initializing || (!serviceLinked && !canInitialize)}
                    onClick={event => {
                        event.stopPropagation();
                        if (serviceLinked) {
                            location.hash = `services/${encodeURIComponent(item.serviceId)}`;
                        }
                        else {
                            onInitialize(item);
                        }
                    }}
                >
                    <span className={initializing ? 'icon spin' : 'icon'} aria-hidden="true">{initializing ? 'refresh' : serviceLinked ? 'dm_service' : 'add_circle'}</span>
                    {initializing ? 'Initializing…' : serviceLinked ? item.service || 'Open linked service' : 'Initialize service'}
                </button>}
            </div>
        </li>;
    })}</ol>;
    return <section className="drawer-section" aria-labelledby="order-items-title"><div className="drawer-section-heading order-items-heading"><div className="drawer-section-title"><h3 id="order-items-title">Order items</h3>{status === 'ready' && <span className="count-pill" data-testid="order-item-count">{items.length}</span>}</div><button className="primary-button add-item-button" type="button" onClick={onAdd} disabled={busy}><span className="icon" aria-hidden="true">add</span>Add item</button></div>{body}</section>;
}

function ServiceOrderItemDetailPanel({ item, onClose, onEdit, onTransition, onSaveConfiguration, onConfirmConfiguration, busy }) {
    const closeButton = useRef(null);
    const [tab, setTab] = useState('item');
    const lifecycle = ['New', 'Acknowledged', 'In Progress', 'Completed'];
    const transitions = getOrderItemTransitions(item.status);
    const currentIndex = lifecycle.findIndex(state => state.toLowerCase() === item.status.trim().toLowerCase());
    useEffect(() => {
        const previous = document.activeElement;
        closeButton.current?.focus();
        const onKey = event => { if (event.key === 'Escape') onClose(); };
        document.addEventListener('keydown', onKey);
        return () => { document.removeEventListener('keydown', onKey); previous?.focus?.(); };
    }, [onClose]);
    const details = [['Name', item.name], ['Action', item.action], ['State', item.status], ['Specification', item.specification], ['Service', item.service], ['Category', item.category], ['Start time', item.start], ['End time', item.end], ['Description', item.description]];
    const transitionActions = <div className="item-transition-actions">{transitions.length === 0 ? <span className="service-related-empty">No transition is available from the current state.</span> : transitions.map(transition => <button className={`item-transition-button ${transition.label === 'Reject' || transition.label === 'Cancel' ? 'danger' : 'primary'}`} type="button" key={`${transition.label}-${transition.next}`} onClick={() => onTransition(item, transition)} disabled={busy}><span className="icon" aria-hidden="true">{transitionIcon(transition.label)}</span>{transition.label}</button>)}</div>;
    return <div className="drawer-layer item-drawer-layer"><div className="drawer-backdrop" onClick={onClose} aria-hidden="true" /><aside className="drawer service-drawer item-detail-drawer" role="dialog" aria-modal="true" aria-labelledby="item-detail-title"><header className="service-drawer-topbar"><nav aria-label="Service order item sections"><button className={tab === 'item' ? 'service-tab active' : 'service-tab'} type="button" onClick={() => setTab('item')} aria-selected={tab === 'item'}><span className="icon" aria-hidden="true">assignment</span>Service Item</button><button className={tab === 'config' ? 'service-tab active' : 'service-tab'} type="button" onClick={() => setTab('config')} aria-selected={tab === 'config'}><span className="icon" aria-hidden="true">settings</span>Configurations</button></nav><button ref={closeButton} className="icon-button" type="button" aria-label="Close service order item details" onClick={onClose}><span className="icon">close</span></button></header>{tab === 'item' ? <div className="service-drawer-body"><div className="service-drawer-heading"><div><p className="eyebrow">Service Order Item</p><h2 id="item-detail-title">{item.name || 'Unnamed item'}</h2></div><button className="quiet-button drawer-action-button" type="button" onClick={onEdit} disabled={busy}><span className="icon" aria-hidden="true">edit</span>Edit</button></div><div className="service-lifecycle item-lifecycle" aria-label="Service order item lifecycle">{lifecycle.map((state, index) => <span className={index === currentIndex ? 'service-lifecycle-step current' : index < currentIndex ? 'service-lifecycle-step complete' : 'service-lifecycle-step'} key={state}>{state}</span>)}</div>{transitionActions}<section className="service-info-card item-info-card"><dl className="detail-grid service-detail-grid">{details.map(([label, value]) => <div key={label} className={label === 'Description' ? 'wide' : ''}><dt>{label}</dt><dd>{value || '—'}</dd></div>)}</dl></section></div> : <NativeServiceConfigurationsView service={item} actionState={{ status: busy ? 'running' : null }} onSave={onSaveConfiguration} onConfirm={onConfirmConfiguration} fetchConfigurations={fetchServiceOrderItemConfigurations} heading="Order item configuration" allowVersionDelete={false} showVersionMetadata={false} supportsProfiles={false} parameterHeading="Configuration parameters" parameterEmptyMessage="No configuration parameters are assigned to this order item." lifecycleMode="OrderItem" />}</aside></div>;
}

function OrderDetailPanel({ order, onClose, onRunAction, onActionFailure, onConfirmDelete, onConfirmAction, onOrdersChanged, onDeleteOrder, onEditOrder, actionState }) {
    const closeButton = useRef(null);
    const [itemsRefreshKey, setItemsRefreshKey] = useState(0);
    const [selectedItem, setSelectedItem] = useState(null);
    const [itemEditor, setItemEditor] = useState(null);
    const [configurationBlockedItemId, setConfigurationBlockedItemId] = useState(null);
    const [initializingItemId, setInitializingItemId] = useState(null);
    const busy = actionState?.status === 'running';
    const transitions = getOrderTransitions(order.state);
    useEffect(() => {
        const previous = document.activeElement;
        closeButton.current?.focus();
        const onKey = event => { if (event.key === 'Escape') onClose(); };
        document.addEventListener('keydown', onKey);
        document.body.classList.add('drawer-open');
        return () => { document.removeEventListener('keydown', onKey); document.body.classList.remove('drawer-open'); previous?.focus?.(); };
    }, [onClose]);
    const facts = [['External ID', order.externalId], ['Priority', order.priority], ['Related organization', order.organization], ['State', order.state]];
    const runOrderEdit = () => onEditOrder(order);
    const runItemAction = async (script, values, label, verifyDeletedId = '') => {
        if (!await onRunAction(script, values, label, async () => {
            if (verifyDeletedId) {
                const refreshedItems = await fetchServiceOrderItems(order.id);
                if (refreshedItems.some(item => item.id === verifyDeletedId)) {
                    throw new Error('The delete script completed, but the order item is still returned by GQI.');
                }
            }
        })) return;
        setItemsRefreshKey(value => value + 1);
    };
    const editItem = item => setItemEditor({ action: 'Edit', item });
    const deleteItem = async item => { if (await onConfirmDelete(`Delete "${item.name || 'this order item'}"?`)) runItemAction(SERVICE_ORDER_SCRIPTS.deleteItem, { 'DOM ID': order.id, 'Service Order Item ID': item.id, 'Skip Confirmation': 'true' }, 'Deleting order item', item.id); };
    const addItem = () => setItemEditor({ action: 'Add', item: null });
    const submitItem = async values => {
        if (!await onRunAction(SERVICE_ORDER_SCRIPTS.addItem, values, itemEditor?.action === 'Edit' ? 'Saving order item' : 'Creating order item', () => fetchServiceOrderItems(order.id))) return;
        setItemEditor(null);
        setSelectedItem(null);
        setItemsRefreshKey(value => value + 1);
    };
    const initializeService = async item => {
        if (!item?.id || item.serviceId || String(item.action || '').trim().toLowerCase() === 'delete') return;
        if (!item.specificationId) {
            onActionFailure('Assign a service specification before initializing a service.');
            return;
        }
        setInitializingItemId(item.id);
        try {
            const specificationConfiguration = await fetchServiceSpecificationConfigurations(item.specificationId);
            const hasConfiguredVersion = (specificationConfiguration?.versions || []).some(version => (version.parameters || []).length > 0 || (version.profiles || []).length > 0);
            if (!hasConfiguredVersion) {
                setConfigurationBlockedItemId(item.id);
                onActionFailure(`Configure the service specification "${item.specification || 'selected specification'}" before initializing a service.`);
                return;
            }
            setConfigurationBlockedItemId(null);
            const initialized = await onRunAction(SERVICE_INVENTORY_SCRIPTS.initializeFromOrderItem, {
                'DOM ID': item.id,
                Action: 'AddItemSilent',
                Payload: JSON.stringify({}),
            }, `Initializing service for ${item.name || 'order item'}`, async () => {
                const refreshedItems = await fetchServiceOrderItems(order.id);
                if (!refreshedItems.some(candidate => candidate.id === item.id && candidate.serviceId)) {
                    throw new Error('The script completed, but the order item is not linked to a Service Inventory service.');
                }
            });
            if (!initialized) return;
            setItemsRefreshKey(value => value + 1);
        }
        catch (error) {
            onActionFailure(error?.message || 'The service initialization could not be verified.');
        }
        finally {
            setInitializingItemId(null);
        }
    };
    const transitionOrder = async (item, transition) => {
        const requiresConfirmation = ['Reject', 'Cancel', 'Confirm Cancellation'].includes(transition.label);
        if (requiresConfirmation && !await onConfirmAction(`Transition "${item.name || 'this service order'}" to ${transition.label.toLowerCase()}?`)) return;
        await onRunAction(SERVICE_ORDER_SCRIPTS.transitionOrder, { PreviousState: transitionStateParameter(item.state), NextState: transition.next, ServiceOrderReference: item.id, 'Skip Confirmation': 'true', 'Cancellation Reason': '' }, `${transition.label} service order`, onOrdersChanged);
    };
    const transitionOrderItem = async (item, transition) => {
        const requiresConfirmation = ['Reject', 'Cancel', 'Confirm Cancellation'].includes(transition.label);
        if (requiresConfirmation && !await onConfirmAction(`Transition "${item.name || 'this order item'}" to ${transition.label.toLowerCase()}?`)) return;
        if (!await onRunAction(SERVICE_ORDER_SCRIPTS.transitionOrderItem, { Id: item.id, PreviousState: transitionStateParameter(item.status), NextState: transition.next, 'Skip Confirmation': 'true' }, `${transition.label} order item`, () => fetchServiceOrderItems(order.id))) return;
        setSelectedItem(null);
        setItemsRefreshKey(value => value + 1);
    };
    const saveItemConfiguration = (values, refresh) => onRunAction(SERVICE_ORDER_SCRIPTS.saveItemConfiguration, values, 'Saving order-item configuration', refresh);
    return <><div className="drawer-layer"><div className="drawer-backdrop" onClick={onClose} aria-hidden="true" /><aside className="drawer" role="dialog" aria-modal="true" aria-labelledby="order-detail-title"><header className="drawer-header"><div className="drawer-title"><p className="eyebrow">Service order</p><h2 id="order-detail-title">{order.name || 'Unnamed order'}</h2><div className="drawer-tags">{order.priority && <span className="order-priority">{order.priority} priority</span>}{order.organization && <span className="order-priority">{order.organization}</span>}</div></div><div className="drawer-header-actions"><button className="quiet-button drawer-action-button" type="button" onClick={runOrderEdit} disabled={busy}><span className="icon" aria-hidden="true">edit</span>Edit</button><button className="danger-button drawer-action-button" type="button" onClick={() => onDeleteOrder(order)} disabled={busy}><span className="icon" aria-hidden="true">delete</span>Delete</button><button ref={closeButton} className="icon-button" type="button" aria-label="Close order details" onClick={onClose}><span className="icon">close</span></button></div></header><div className="drawer-body"><section className="drawer-section" aria-labelledby="order-info-title"><div className="drawer-section-heading"><h3 id="order-info-title">Service order details</h3></div><div className="order-transition-panel"><div className="order-transition-panel-heading"><span className="eyebrow">Current state</span><span className={`state-badge ${stateTone(order.state)} order-transition-current`}>{order.state}</span></div><div className="order-transition-actions">{transitions.length === 0 ? <span className="service-related-empty">No transition is available from the current state.</span> : transitions.map(transition => <button className={`order-transition-action ${transition.label === 'Reject' || transition.label === 'Cancel' ? 'danger' : 'primary'}`} type="button" key={`${transition.label}-${transition.next}`} onClick={() => transitionOrder(order, transition)} disabled={busy}><span className="icon" aria-hidden="true">{transitionIcon(transition.label)}</span><span>{transition.label === 'Partially Failed' ? 'Partial fail' : transition.label === 'Information Missing' ? 'Missing info' : transition.label}</span></button>)}</div></div><dl className="detail-grid">{facts.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value || '—'}</dd></div>)}<div className="wide"><dt>Description</dt><dd>{order.description || 'No description'}</dd></div></dl></section><OrderItems orderId={order.id} refreshKey={itemsRefreshKey} onAdd={addItem} onEdit={editItem} onDelete={deleteItem} onSelect={setSelectedItem} onInitialize={initializeService} initializingItemId={initializingItemId} configurationBlockedItemId={configurationBlockedItemId} busy={busy} /></div></aside>{selectedItem && <ServiceOrderItemDetailPanel item={selectedItem} onClose={() => setSelectedItem(null)} onEdit={() => editItem(selectedItem)} onTransition={transitionOrderItem} onSaveConfiguration={saveItemConfiguration} onConfirmConfiguration={onConfirmAction} busy={busy} />}</div>{itemEditor && <ServiceOrderItemDialog key={`${itemEditor.action}-${itemEditor.item?.id || 'new'}`} busy={actionState?.status === 'running'} orderId={order.id} action={itemEditor.action} initialItem={itemEditor.item} onClose={() => setItemEditor(null)} onSubmit={submitItem} />}</>;
}

function OrdersPage({ selectedId }) {
    const { status, orders, error, updatedAt, reload, reloadAndWait } = useServiceOrders();
    const { state: actionState, run: runAction, fail: failAction, dismiss: dismissAction } = useAutomationActions();
    const { confirm, dialog: confirmationDialog } = useConfirmation();
    const [search, setSearch] = useState('');
    const [statusFilter, setStatusFilter] = useState('all');
    const [sort, setSort] = useState({ key: 'default', direction: 'asc' });
    const [newOrderOpen, setNewOrderOpen] = useState(false);
    const [ordersPage, setOrdersPage] = useState(1);
    const [ordersPageSize, setOrdersPageSize] = useState(10);
    const [editOrderTarget, setEditOrderTarget] = useState(null);
    const loading = status === 'loading';
    const hasData = status === 'ready' || (loading && orders.length > 0);
    const openCount = orders.filter(isOpenOrder).length;
    const attentionCount = orders.filter(isAttentionOrder).length;
    const completedCount = orders.filter(isCompletedOrder).length;
    const selectedOrder = selectedId ? orders.find(order => order.id === selectedId) : null;
    const isPreviewData = import.meta.env.MODE === 'mock' || import.meta.env.VITE_USE_MOCK === 'true';
    const closeDetails = useCallback(() => { location.hash = 'orders'; }, []);
    const createOrder = () => setNewOrderOpen(true);
    const submitNewOrder = async values => {
        if (await runAction(SERVICE_ORDER_SCRIPTS.createOrder, values, 'Creating service order', reloadAndWait)) {
            setNewOrderOpen(false);
        }
    };
    const editOrder = order => setEditOrderTarget(order);
    const submitEditedOrder = async values => {
        if (await runAction(SERVICE_ORDER_SCRIPTS.createOrder, values, 'Saving service order', reloadAndWait)) {
            setEditOrderTarget(null);
        }
    };
    const deleteOrder = async order => {
        if (!await confirm(`Delete "${order.name || 'this service order'}"?`)) return;
        if (await runAction(SERVICE_ORDER_SCRIPTS.deleteOrder, { 'DOM ID': order.id, 'Skip Confirmation': 'true' }, 'Deleting service order', async () => {
            const refreshedOrders = await reloadAndWait();
            if (refreshedOrders.some(candidate => candidate.id === order.id)) {
                throw new Error('The delete script completed, but the service order is still returned by GQI.');
            }
        })) {
            closeDetails();
        }
    };
    const normalizedSearch = search.trim().toLowerCase();
    const filteredOrders = orders.filter(order => {
        const matchesStatus = statusFilter === 'all' || (statusFilter === 'open' && isOpenOrder(order)) || (statusFilter === 'attention' && isAttentionOrder(order)) || (statusFilter === 'completed' && isCompletedOrder(order));
        const searchable = [order.name, order.externalId, order.organization, order.description, order.state, order.priority].join(' ').toLowerCase();
        return matchesStatus && (!normalizedSearch || searchable.includes(normalizedSearch));
    });
    const visibleOrders = [...filteredOrders].sort((left, right) => {
        const creationComparison = orderStartTimestamp(right) - orderStartTimestamp(left);
        return creationComparison || left.name.localeCompare(right.name, undefined, { sensitivity: 'base' });
    });
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
    useEffect(() => { setOrdersPage(1); }, [search, statusFilter, sort.key, sort.direction]);
    useEffect(() => { setOrdersPage(current => Math.min(current, Math.max(1, Math.ceil(filteredOrders.length / ordersPageSize)))); }, [filteredOrders.length, ordersPageSize]);
    const pagedOrders = visibleOrders.slice((ordersPage - 1) * ordersPageSize, ordersPage * ordersPageSize);
    const toggleSort = key => setSort(current => current.key === key ? { key, direction: current.direction === 'asc' ? 'desc' : 'asc' } : { key, direction: 'asc' });
    const clearFilters = () => { setSearch(''); setStatusFilter('all'); };
    const sortIndicator = key => <span className={`table-sort-icon ${sort.key === key ? sort.direction : 'muted'}`} aria-hidden="true" />;
    const sortButton = (key, label) => <button className="table-sort" type="button" onClick={() => toggleSort(key)}>{label}{sortIndicator(key)}</button>;
    const openOrder = order => { if (order.id) location.hash = `orders/${encodeURIComponent(order.id)}`; };
    const onOrderKeyDown = (event, order) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); openOrder(order); } };
    let body;
    if (status === 'error') body = <div className="list-message error" role="alert"><span className="icon" aria-hidden="true">error</span><div className="list-message-text"><strong>Service orders could not be loaded</strong><span>{error}</span></div><button className="quiet-button" type="button" onClick={reload}><span className="icon" aria-hidden="true">refresh</span>Retry</button></div>;
    else if (!hasData) body = <div className="list-message" role="status"><span className="icon spin" aria-hidden="true">refresh</span>Loading service orders…</div>;
    else if (orders.length === 0) body = <div className="list-message" role="status"><span className="icon" aria-hidden="true">inbox</span>No service orders yet.</div>;
    else if (filteredOrders.length === 0) body = <div className="orders-empty" role="status"><span className="icon" aria-hidden="true">search_off</span><div><strong>No orders match these filters</strong><span>Try a different search or status.</span></div><button className="quiet-button" type="button" onClick={clearFilters}>Clear filters</button></div>;
    else body = <div className="orders-table-wrap" aria-busy={loading}><table className="orders-table"><colgroup><col className="order-column" /><col className="organization-column" /><col className="priority-column" /><col className="state-column" /><col className="description-column" /><col className="action-column" /></colgroup><thead><tr><th scope="col">{sortButton('name', 'Order')}</th><th scope="col">{sortButton('organization', 'Organization')}</th><th scope="col">{sortButton('priority', 'Priority')}</th><th scope="col">{sortButton('state', 'State')}</th><th scope="col">Description</th><th scope="col"><span className="sr-only">Open</span></th></tr></thead><tbody>{pagedOrders.map((order, index) => <tr className={order.id && order.id === selectedId ? 'order-row selected' : 'order-row'} key={`${order.id || order.name || 'order'}-${index}`} onDoubleClick={() => openOrder(order)}><td><div className="table-order-cell"><span className="order-icon"><span className="icon" aria-hidden="true">assignment</span></span><span className="table-cell-main"><strong>{order.name || 'Unnamed order'}</strong><span>{orderReference(order) || 'No external ID'}</span></span></div></td><td><span className="table-primary">{order.organization || '—'}</span></td><td><span className={`priority-chip ${order.priority.trim().toLowerCase() || 'none'}`}><span className="priority-dot" aria-hidden="true" />{order.priority ? `${order.priority} priority` : 'No priority'}</span></td><td><span className={`state-badge ${stateTone(order.state)}`}>{order.state || 'Unknown'}</span></td><td><span className="table-description" title={order.description || undefined}>{order.description || 'No description'}</span></td><td className="table-action"><button className="table-row-open" type="button" aria-label={`Open details of ${order.name || 'unnamed order'}`} onClick={() => openOrder(order)}><span className="icon" aria-hidden="true">chevron_right</span></button></td></tr>)}</tbody></table></div>;
    return <main className="page-content orders-page">
        <section className="orders-hero">
            <div className="orders-heading"><div className="orders-eyebrow"><p className="eyebrow">{pages.orders.eyebrow}</p><span className={isPreviewData ? 'live-indicator preview' : 'live-indicator'}><span aria-hidden="true" />{isPreviewData ? 'Preview data' : 'DataMiner data'}</span></div><h1>Orders</h1><p className="description">A clear view of every service order, from request to completion.</p></div>
            <PageHeaderActions
                updatedAt={updatedAt}
                loading={loading}
                onRefresh={reload}
                primaryAction={<button className="primary-button orders-new" type="button" onClick={createOrder} disabled={actionState?.status === 'running'}><span className="icon" aria-hidden="true">add</span>New order</button>}
            />
        </section>
        <ActionNotice state={actionState} onDismiss={dismissAction} />
        {confirmationDialog}
        {newOrderOpen && <NewOrderDialog busy={actionState?.status === 'running'} existingOrderNames={orders.map(order => order.name)} onClose={() => setNewOrderOpen(false)} onSubmit={submitNewOrder} />}
        {editOrderTarget && <NewOrderDialog key={`edit-order-${editOrderTarget.id}`} busy={actionState?.status === 'running'} action="Edit" existingOrderNames={orders.map(order => order.name)} initialOrder={editOrderTarget} onClose={() => setEditOrderTarget(null)} onSubmit={submitEditedOrder} />}
        <section className="order-stats" aria-label="Orders overview"><button className={statusFilter === 'all' ? 'order-stat selected' : 'order-stat'} type="button" aria-pressed={statusFilter === 'all'} onClick={() => setStatusFilter('all')}><span className="overview-metric-top"><span className="overview-metric-icon icon" aria-hidden="true">assignment</span><span className="order-stat-label">All orders</span></span><strong>{hasData ? orders.length : '–'}</strong><span className="order-stat-note">Total in workspace</span></button><button className={statusFilter === 'open' ? 'order-stat selected' : 'order-stat'} type="button" aria-pressed={statusFilter === 'open'} onClick={() => setStatusFilter('open')}><span className="overview-metric-top"><span className="overview-metric-icon icon" aria-hidden="true">play_circle</span><span className="order-stat-label">Open orders</span></span><strong data-testid="open-orders">{hasData ? openCount : '–'}</strong><span className="order-stat-note">{hasData ? 'Still in progress' : 'Waiting for data'}</span></button><button className={statusFilter === 'attention' ? 'order-stat selected' : 'order-stat'} type="button" aria-pressed={statusFilter === 'attention'} onClick={() => setStatusFilter('attention')}><span className="overview-metric-top"><span className="overview-metric-icon icon" aria-hidden="true">error</span><span className="order-stat-label">Orders needing attention</span></span><strong>{hasData ? attentionCount : '–'}</strong><span className="order-stat-note">Held, failed, rejected, or cancelling</span></button><button className={statusFilter === 'completed' ? 'order-stat selected' : 'order-stat'} type="button" aria-pressed={statusFilter === 'completed'} onClick={() => setStatusFilter('completed')}><span className="overview-metric-top"><span className="overview-metric-icon icon" aria-hidden="true">check_circle</span><span className="order-stat-label">Completed</span></span><strong>{hasData ? completedCount : '–'}</strong><span className="order-stat-note">Successfully delivered</span></button></section>
        <section className="orders-surface" aria-labelledby="orders-table-title"><div className="orders-toolbar"><div className="orders-toolbar-title"><div><p className="eyebrow">Service orders</p><h2 id="orders-table-title">All orders</h2></div>{hasData && <span className="orders-total-copy">{orders.length} {orders.length === 1 ? 'order' : 'orders'} in total</span>}</div><div className="orders-toolbar-controls"><label className="orders-search"><span className="icon" aria-hidden="true">search</span><span className="sr-only">Search orders</span><input type="search" value={search} onChange={event => setSearch(event.target.value)} placeholder="Search orders" aria-label="Search orders" />{search && <button type="button" aria-label="Clear search" onClick={() => setSearch('')}><span className="icon" aria-hidden="true">close</span></button>}</label><label className="orders-filter"><span className="sr-only">Filter orders by status</span><select value={statusFilter} onChange={event => setStatusFilter(event.target.value)} aria-label="Filter orders by status"><option value="all">All statuses</option><option value="open">Open</option><option value="attention">Needs attention</option><option value="completed">Completed</option></select><span className="icon" aria-hidden="true">expand_more</span></label></div></div>{body}</section>
        <TablePagination page={ordersPage} pageSize={ordersPageSize} total={filteredOrders.length} onPageChange={setOrdersPage} onPageSizeChange={size => { setOrdersPageSize(size); setOrdersPage(1); }} />
        {selectedOrder && <OrderDetailPanel key={selectedOrder.id} order={selectedOrder} onClose={closeDetails} onRunAction={runAction} onActionFailure={failAction} onConfirmDelete={confirm} onConfirmAction={confirm} onOrdersChanged={reloadAndWait} onDeleteOrder={deleteOrder} onEditOrder={editOrder} actionState={actionState} />}
    </main>;
}

function SettingsPage() {
    const { theme, setTheme, resolvedTheme } = useTheme();
    return <main className="page-content">
        <div className="page-heading"><div><p className="eyebrow">Workspace</p><h1>Settings</h1><p className="description">Tune the workspace to fit the way you work.</p></div></div>
        <section className="settings-section"><div><p className="eyebrow">Appearance</p><h2>Theme</h2><p className="setting-description">System mode follows your operating system preference automatically.</p></div><div className="theme-picker" role="group" aria-label="Theme"><button className={theme === 'system' ? 'theme-choice active' : 'theme-choice'} type="button" onClick={() => setTheme('system')}><span className="icon">contrast</span>System</button><button className={theme === 'light' ? 'theme-choice active' : 'theme-choice'} type="button" onClick={() => setTheme('light')}><span className="icon">light_mode</span>Light</button><button className={theme === 'dark' ? 'theme-choice active' : 'theme-choice'} type="button" onClick={() => setTheme('dark')}><span className="icon">dark_mode</span>Dark</button></div><p className="resolved-theme">Currently using <strong>{resolvedTheme}</strong> appearance</p></section>
        <section className="settings-section compact"><div><p className="eyebrow">Service catalog</p><h2>Category management</h2><p className="setting-description">Define the categories and logos used by the Service Inventory.</p></div><a className="quiet-button" href="#catalog/categories"><span className="icon" aria-hidden="true">category</span>Manage categories</a></section>
        <section className="settings-section compact"><div><p className="eyebrow">Session</p><h2>DataMiner connection</h2><p className="setting-description">Your session is managed by DataMiner authentication.</p></div><button className="quiet-button" type="button" onClick={() => location.replace(`/auth/logout?url=${encodeURIComponent(location.pathname + location.search)}`)}>Sign out <span className="icon">logout</span></button></section>
    </main>;
}

function parseRoute() { const [page, ...rest] = location.hash.slice(1).split('/'); const key = pages[page] ? page : 'overview'; return { page: key, param: key === page ? decodeURIComponent(rest.join('/')) : '' }; }

function App() {
    const [route, setRoute] = useState(parseRoute);
    const { theme, setTheme } = useTheme();
    useEffect(() => { const onHash = () => setRoute(parseRoute()); window.addEventListener('hashchange', onHash); return () => window.removeEventListener('hashchange', onHash); }, []);
    const page = route.page;
    const activePage = page === 'catalog' && route.param === 'categories' ? 'settings' : page;
    return <div className="app-shell"><header className="app-header"><a className="brand" href="#overview" aria-label="Service Management home"><img src="./references/dataminer-logo.png" alt="DataMiner" /><span className="brand-divider" /><strong>Service Management</strong></a><nav className="main-nav" aria-label="Primary navigation">{Object.entries(pages).filter(([key]) => key !== 'overview').map(([key, value]) => <a className={activePage === key ? 'nav-link active' : 'nav-link'} href={`#${key}`} key={key}><span className="icon">{value.icon}</span>{value.label}</a>)}</nav><div className="header-actions"><ThemeMenu theme={theme} setTheme={setTheme} /><button className="avatar" type="button" aria-label="Account">SM</button></div></header><PageContent page={page} param={route.param} /></div>;
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