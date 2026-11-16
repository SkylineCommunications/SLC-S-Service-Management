const MEDIAOPS_JOB_URL = '/app/ab26de0d-b0d7-456c-9c7b-155008a5cbc9/Job%20Panel?object%20manager%20instances=(slc)workflow%2F{jobId}';

export function mediaOpsJobUrl(jobId) {
    return new URL(MEDIAOPS_JOB_URL.replace('{jobId}', encodeURIComponent(jobId)), window.location.origin).href;
}
