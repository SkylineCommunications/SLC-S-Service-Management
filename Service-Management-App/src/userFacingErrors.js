const UUID_PATTERN = '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}';
const UUID = new RegExp(UUID_PATTERN, 'gi');
const QUOTED_UUID = new RegExp(`(['"])${UUID_PATTERN}\\1`, 'gi');
const NAMED_UUID = new RegExp(`\\b(service order item|service order|service item|service specification|service category|service profile|profile parameter|profile definition|nested profile|configuration parameter value|configuration parameter|configuration version|configuration|service|category|profile)(?:\\s+id)?\\s+(['"])${UUID_PATTERN}\\2`, 'gi');
const GENERIC_ID_UUID = new RegExp(`\\b(?:id|identifier)\\s+(['"])${UUID_PATTERN}\\1`, 'gi');

/**
 * Converts technical API identifiers into wording suitable for a user-facing message.
 * Backend messages that already contain a name or label are left unchanged.
 */
export function userFacingErrorMessage(error, fallback = 'An unexpected error occurred.') {
    const raw = typeof error === 'string' ? error : error?.message;
    if (raw === undefined || raw === null || !String(raw).trim()) return fallback;

    let message = String(raw).trim()
        .replace(NAMED_UUID, '$1')
        .replace(GENERIC_ID_UUID, 'the selected item')
        .replace(QUOTED_UUID, 'the selected item')
        .replace(UUID, 'the selected item');

    return message || fallback;
}
