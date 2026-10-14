// DataMiner session helpers shared by the app shell and the Web API data modules.
export function getConnectionFromCookie() { const match = document.cookie.match(/(?:^|;\s*)DMAConnection=([^;]+)/); return match ? decodeURIComponent(match[1]) : null; }
export function redirectToAuth() { const target = `${location.pathname}${location.search}${location.hash}`; location.replace(`/auth/?url=${encodeURIComponent(target)}`); }
