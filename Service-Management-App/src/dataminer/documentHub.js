import { executeAutomationScript, readScriptOutputs } from './gqi.js';

const DOCUMENT_HUB_SCRIPT = {
    name: 'SLC_SM_AS_GetDocumentHubLogos',
    folder: 'Service Management',
    interactive: false,
    parameterDefinitions: [
        { id: 2, name: 'Bucket Name' },
        { id: 3, name: 'Name Filter' },
    ],
};

export async function fetchDocumentHubLogos({ bucketName = '', nameFilter = '' } = {}) {
    const result = await executeAutomationScript(DOCUMENT_HUB_SCRIPT, {
        'Bucket Name': bucketName,
        'Name Filter': nameFilter,
    });
    const response = readScriptOutputs(result).Response;
    let files;
    try {
        files = response ? JSON.parse(response) : [];
    } catch {
        throw new Error('Document Hub returned an invalid file list.');
    }

    if (!Array.isArray(files)) {
        throw new Error('Document Hub returned an invalid file list.');
    }

    return files
        .map(file => ({ id: file.icon ?? file.fileName ?? '', name: file.fileName ?? '', icon: file.icon ?? '' }))
        .filter(logo => logo.name.trim() && logo.icon.trim());
}
