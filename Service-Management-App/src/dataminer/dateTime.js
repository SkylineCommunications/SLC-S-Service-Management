export function formatDataMinerDateTime(value) {
    if (value === null || value === undefined || String(value).trim() === '') return '';

    const raw = String(value).trim();
    const numeric = Number(raw);
    const date = Number.isFinite(numeric) && numeric > 100000000000
        ? new Date(numeric)
        : new Date(raw);

    if (Number.isNaN(date.getTime())) return raw;

    const hour = date.getHours();
    const suffix = hour >= 12 ? 'PM' : 'AM';
    const hour12 = hour % 12 || 12;
    const pad = number => String(number).padStart(2, '0');
    return `${date.getMonth() + 1}/${date.getDate()}/${date.getFullYear()} ${hour12}:${pad(date.getMinutes())}:${pad(date.getSeconds())} ${suffix}`;
}

export function getDataMinerTimestamp(value) {
    if (value === null || value === undefined || String(value).trim() === '') return 0;
    const raw = String(value).trim();
    const numeric = Number(raw);
    if (Number.isFinite(numeric) && numeric > 100000000000) return numeric;
    const parsed = Date.parse(raw);
    return Number.isNaN(parsed) ? 0 : parsed;
}
