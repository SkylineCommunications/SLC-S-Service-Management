import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { IasSession, layoutComponents } from './dataminer/ias.js';

const CANCEL_PATTERN = /^(cancel|close|abort)\b/i;

function isBlankStatic(component) { return component.type === 'static' && !component.text.trim(); }
function isButtonRow(row) {
    const components = row.flatMap(cell => cell.components);
    return components.some(component => component.type === 'button') && components.every(component => component.type === 'button' || isBlankStatic(component));
}

// The trailing row that only holds buttons (e.g. Create/Cancel) becomes the dialog footer.
function splitFooter(rows) {
    let end = rows.length;
    while (end > 0 && rows[end - 1].flatMap(cell => cell.components).every(isBlankStatic)) end--;
    if (end === 0 || !isButtonRow(rows[end - 1])) return { body: rows, footer: [] };
    return { body: rows.slice(0, end - 1), footer: rows[end - 1].flatMap(cell => cell.components).filter(component => component.type === 'button') };
}

function gridTemplate(rows) {
    const columnCount = Math.max(1, ...rows.map(row => row.reduce((sum, cell) => sum + cell.colSpan, 0)));
    const kinds = Array(columnCount).fill('empty');
    for (const row of rows) {
        let column = 0;
        for (const cell of row) {
            const visible = cell.components.filter(component => component.visible && !isBlankStatic(component));
            if (visible.length) {
                const kind = visible.every(component => component.type === 'static') ? 'label' : 'input';
                for (let index = column; index < column + cell.colSpan && index < columnCount; index++) {
                    if (cell.colSpan === 1 || kinds[index] === 'empty') kinds[index] = kinds[index] === 'input' ? 'input' : kind === 'input' || cell.colSpan > 1 ? 'input' : 'label';
                }
            }
            column += cell.colSpan;
        }
    }
    return { columnCount, template: kinds.map(kind => kind === 'label' ? 'minmax(96px, max-content)' : kind === 'input' ? 'minmax(0, 1fr)' : 'minmax(0, 8px)').join(' ') };
}

function parseIasDate(value) { return value ? Date.parse(String(value).replace(/(\.\d{3})\d+/, '$1')) : NaN; }
function toLocalInput(value) {
    const time = parseIasDate(value);
    if (!Number.isFinite(time)) return '';
    const date = new Date(time);
    const pad = number => String(number).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}
function fromLocalInput(value) {
    const time = Date.parse(value);
    return Number.isFinite(time) ? new Date(time).toISOString().replace(/\.\d{3}Z$/, '.0000000Z') : '';
}

function initialValues(layout) {
    const values = {};
    for (const component of layoutComponents(layout)) {
        if (!component.destVar || component.type === 'button') continue;
        let value = component.value;
        if (component.type === 'dropdown' && value && !component.options.some(option => option.key === value)) {
            value = component.options.find(option => option.label === value)?.key ?? value;
        }
        values[component.destVar] = value;
    }
    return values;
}

function fieldLabels(layout) {
    const labels = new Map();
    for (const row of layout.rows) {
        let last = '';
        for (const cell of row) for (const component of cell.components) {
            if (component.type === 'static' && component.text.trim()) last = component.text.trim();
            else if (component.destVar && component.type !== 'button') labels.set(component.destVar, last || component.text || component.debugTag || component.type);
        }
    }
    return labels;
}

/**
 * Runs an interactive Automation script and renders its dialogs in the app.
 * `onFinished({ cancelled, result })` is called once the script completes.
 */
export function IasDialog({ script, parameters, eyebrow = 'Interactive Automation script', onClose, onFinished }) {
    const [state, setState] = useState({ phase: 'starting' });
    const [values, setValues] = useState({});
    const sessionRef = useRef(null);
    const valuesRef = useRef(values);
    const dirty = useRef(new Set());
    const queuedButton = useRef(null);
    const lastTrigger = useRef('');
    const finishedRef = useRef(false);
    const dialogRef = useRef(null);
    valuesRef.current = values;

    useEffect(() => {
        const session = new IasSession(script, parameters);
        sessionRef.current = session;
        const unsubscribe = session.subscribe(setState);
        session.start();
        return () => { unsubscribe(); session.close(); };
        // The session is launched once per mounted dialog.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    const layout = state.phase === 'interactive' ? state.layout : null;
    const version = state.phase === 'interactive' ? state.version : 0;
    useEffect(() => {
        if (!layout) return;
        setValues(initialValues(layout));
        dirty.current.clear();
        // Only a new server render resets the form; async dropdown updates keep the user's edits.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [version]);

    const busy = state.phase === 'interactive' && (state.busy || state.pendingAsync > 0);

    useEffect(() => {
        if (state.phase !== 'done' || finishedRef.current) return;
        finishedRef.current = true;
        onFinished?.({ cancelled: CANCEL_PATTERN.test(lastTrigger.current), result: state.result });
    }, [state, onFinished]);

    useEffect(() => {
        const trigger = queuedButton.current;
        if (!layout || busy || !trigger) return;
        queuedButton.current = null;
        const button = layoutComponents(layout).find(component => component.destVar === trigger && component.type === 'button' && component.enabled);
        if (button) { lastTrigger.current = button.text; sessionRef.current?.submit(valuesRef.current, trigger); }
    }, [layout, busy]);

    useLayoutEffect(() => {
        const dialog = dialogRef.current;
        if (!dialog) return;
        if (!dialog.open) dialog.showModal();
        return () => { if (dialog.open) dialog.close(); };
    }, []);

    const labels = useMemo(() => layout ? fieldLabels(layout) : new Map(), [layout]);
    const { body, footer } = useMemo(() => layout ? splitFooter(layout.rows) : { body: [], footer: [] }, [layout]);
    const grid = useMemo(() => gridTemplate(body), [body]);

    const close = () => {
        sessionRef.current?.close();
        if (dialogRef.current?.open) dialogRef.current.close();
        onClose();
    };
    const onCancel = event => {
        event.preventDefault();
        close();
    };
    const stopParentEscape = event => {
        if (event.key === 'Escape') event.stopPropagation();
    };

    const setValue = (destVar, value) => { dirty.current.add(destVar); setValues(current => ({ ...current, [destVar]: value })); };
    const submit = (trigger, kind = 'change', override = {}) => sessionRef.current?.submit({ ...valuesRef.current, ...override }, trigger, kind);
    const commitText = component => {
        if (!component.destVar || !dirty.current.has(component.destVar)) return;
        dirty.current.delete(component.destVar);
        if (component.wantsOnFocusLost) submit(component.destVar, 'focuslost');
        else if (component.wantsOnChange) submit(component.destVar, 'change');
    };
    const pressButton = component => {
        if (busy) { queuedButton.current = component.destVar; return; }
        lastTrigger.current = component.text;
        submit(component.destVar);
    };

    const renderComponent = (component, key) => {
        if (!component.visible) return null;
        const destVar = component.destVar || '';
        const disabled = busy || !component.enabled || component.readOnly;
        const invalid = component.validationState.toLowerCase() === 'invalid';
        const label = labels.get(destVar);
        const help = invalid && component.validationText ? <span className="ias-validation">{component.validationText}</span> : null;
        const common = { 'aria-label': label, 'aria-invalid': invalid || undefined, 'data-debug-tag': component.debugTag || undefined, title: component.tooltip || undefined };
        switch (component.type) {
            case 'static': {
                if (!component.text.trim()) return <div key={key} className="ias-spacer" aria-hidden="true" />;
                if (['title1', 'title2', 'bold'].includes(component.className)) return <h3 key={key} className="ias-section-title">{component.text}</h3>;
                return <span key={key} className="ias-text" title={component.tooltip || undefined}>{component.text}</span>;
            }
            case 'textbox':
            case 'password':
                return <div key={key} className="ias-field">{component.multiline
                    ? <textarea {...common} value={values[destVar] ?? ''} placeholder={component.placeholder || undefined} disabled={disabled} onChange={event => setValue(destVar, event.target.value)} onBlur={() => commitText(component)} />
                    : <input {...common} type={component.type === 'password' ? 'password' : 'text'} value={values[destVar] ?? ''} placeholder={component.placeholder || undefined} disabled={disabled} onChange={event => setValue(destVar, event.target.value)} onBlur={() => commitText(component)} onKeyDown={event => { if (event.key === 'Enter') { event.preventDefault(); commitText(component); } }} />}{help}</div>;
            case 'numeric':
                return <div key={key} className="ias-field"><input {...common} type="number" value={values[destVar] ?? ''} disabled={disabled} onChange={event => setValue(destVar, event.target.value)} onBlur={() => commitText(component)} />{help}</div>;
            case 'checkbox':
                return <label key={key} className="ias-check" title={component.tooltip || undefined}><input type="checkbox" aria-label={label || component.text || undefined} data-debug-tag={component.debugTag || undefined} checked={/^true$/i.test(values[destVar] ?? '')} disabled={disabled} onChange={event => {
                    const value = event.target.checked ? 'True' : 'False';
                    setValue(destVar, value);
                    if (component.wantsOnChange) submit(destVar, 'change', { [destVar]: value });
                }} />{component.text && <span>{component.text}</span>}</label>;
            case 'checkboxlist': {
                const selected = new Set(String(values[destVar] ?? '').split(';').filter(Boolean));
                return <div key={key} className="ias-checklist" role="group" aria-label={label || 'Options'} data-debug-tag={component.debugTag || undefined}>{component.options.length === 0 ? <span className="ias-empty">No options available</span> : component.options.map(option => <label key={option.key} className="ias-check"><input type="checkbox" checked={selected.has(option.key)} disabled={disabled} onChange={event => {
                    const next = new Set(selected);
                    if (event.target.checked) next.add(option.key); else next.delete(option.key);
                    const value = component.options.filter(candidate => next.has(candidate.key)).map(candidate => candidate.key).join(';');
                    setValue(destVar, value);
                    if (component.wantsOnChange) submit(destVar, 'change', { [destVar]: value });
                }} /><span>{option.label}</span></label>)}</div>;
            }
            case 'dropdown': {
                const value = values[destVar] ?? '';
                const options = component.options.some(option => option.key === value) || !value ? component.options : [{ key: value, label: value }, ...component.options];
                return <div key={key} className="ias-field"><select {...common} value={value} disabled={disabled || (state.pendingAsync > 0 && options.length === 0)} onChange={event => {
                    setValue(destVar, event.target.value);
                    if (component.wantsOnChange) submit(destVar, 'change', { [destVar]: event.target.value });
                }}>{options.length === 0 && <option value="">{state.pendingAsync > 0 ? 'Loading…' : 'No options'}</option>}{options.map(option => <option key={option.key} value={option.key}>{option.label}</option>)}</select>{help}</div>;
            }
            case 'time':
                return <div key={key} className="ias-field"><input {...common} type={component.includeTime ? 'datetime-local' : 'date'} value={component.includeTime ? toLocalInput(values[destVar]) : toLocalInput(values[destVar]).slice(0, 10)} disabled={disabled} onChange={event => {
                    const value = fromLocalInput(component.includeTime ? event.target.value : `${event.target.value}T00:00`);
                    setValue(destVar, value);
                    if (component.wantsOnChange) submit(destVar, 'change', { [destVar]: value });
                }} />{help}</div>;
            case 'button': {
                const primary = component.className === 'calltoaction';
                return <button key={key} type="button" className={primary ? 'primary-button' : 'quiet-button'} data-debug-tag={component.debugTag || undefined} title={component.tooltip || undefined} disabled={!component.enabled} onClick={() => pressButton(component)}>{component.text || 'Continue'}</button>;
            }
            default:
                return <div key={key} className="ias-field"><input {...common} value={values[destVar] ?? component.text} disabled={disabled} onChange={event => setValue(destVar, event.target.value)} onBlur={() => commitText(component)} data-ias-type={component.type} /></div>;
        }
    };

    const title = layout?.title || (state.phase === 'progress' ? state.title : '') || script.name;
    let content;
    if (state.phase === 'starting') content = <div className="ias-status" role="status"><span className="icon spin" aria-hidden="true">refresh</span>Launching {script.name}…</div>;
    else if (state.phase === 'progress') content = <div className="ias-status" role="status"><span className="icon spin" aria-hidden="true">refresh</span>{state.message}</div>;
    else if (state.phase === 'failed') content = <div className="ias-status error" role="alert"><span className="icon" aria-hidden="true">error</span><span>{state.error || 'The interactive script failed.'}</span></div>;
    else if (state.phase === 'done') content = <div className="ias-status success" role="status"><span className="icon" aria-hidden="true">check_circle</span>The script completed.</div>;
    else if (state.phase === 'closed') content = <div className="ias-status" role="status">The script was closed.</div>;
    else content = <form className="ias-grid" style={{ gridTemplateColumns: grid.template }} onSubmit={event => event.preventDefault()} aria-busy={busy || undefined}>{body.map((row, rowIndex) => {
        let column = 0;
        return row.map((cell, cellIndex) => {
            const start = column;
            column += cell.colSpan;
            if (!cell.components.some(component => component.visible)) return null;
            return <div key={`${rowIndex}-${cellIndex}`} className="ias-cell" style={{ gridColumn: `${start + 1} / span ${cell.colSpan}`, gridRow: `${rowIndex + 1} / span ${cell.rowSpan}` }}>{cell.components.map((component, index) => renderComponent(component, `${rowIndex}-${cellIndex}-${index}`))}</div>;
        });
    })}</form>;

    return (
        <dialog ref={dialogRef} className="confirm-layer ias-layer" aria-modal="true" aria-labelledby="ias-dialog-title" onCancel={onCancel} onKeyDown={stopParentEscape}>
            <div className="confirm-backdrop" onClick={close} aria-hidden="true" />
            <section className="order-form-dialog ias-dialog">
                <header className="order-form-header">
                    <div>
                        <p className="eyebrow">{eyebrow}</p>
                        <h2 id="ias-dialog-title">{title}</h2>
                    </div>
                    <button className="icon-button" type="button" aria-label="Close interactive script" onClick={close}>
                        <span className="icon" aria-hidden="true">close</span>
                    </button>
                </header>
                <div className="ias-body">{content}</div>
                {state.phase === 'interactive' && footer.length > 0 && <footer className="order-form-actions ias-footer">{busy && <span className="ias-busy" role="status"><span className="icon spin" aria-hidden="true">refresh</span>{state.pendingAsync > 0 ? 'Loading options…' : 'Updating…'}</span>}{footer.map((component, index) => renderComponent(component, `footer-${index}`))}</footer>}
                {state.phase === 'interactive' && footer.length === 0 && busy && <footer className="order-form-actions ias-footer"><span className="ias-busy" role="status"><span className="icon spin" aria-hidden="true">refresh</span>Updating…</span></footer>}
                {(state.phase === 'failed' || state.phase === 'closed' || state.phase === 'done') && <footer className="order-form-actions ias-footer"><button className="quiet-button" type="button" onClick={close}>Close</button></footer>}
            </section>
        </dialog>
    );
}
