import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { formatDataMinerDateTime, getDataMinerTimestamp } from './dateTime.js';

describe('formatDataMinerDateTime', () => {
    it('returns an empty string for missing values', () => {
        for (const value of [null, undefined, '', '   ']) assert.equal(formatDataMinerDateTime(value), '');
    });

    it('formats epoch milliseconds as M/D/YYYY h:mm:ss AM/PM in local time', () => {
        const afternoon = new Date(2026, 0, 5, 13, 4, 9).getTime();
        assert.equal(formatDataMinerDateTime(afternoon), '1/5/2026 1:04:09 PM');
        assert.equal(formatDataMinerDateTime(String(afternoon)), '1/5/2026 1:04:09 PM');
    });

    it('shows midnight and noon as 12', () => {
        assert.equal(formatDataMinerDateTime(new Date(2026, 10, 7, 0, 0, 0).getTime()), '11/7/2026 12:00:00 AM');
        assert.equal(formatDataMinerDateTime(new Date(2026, 10, 7, 12, 30, 0).getTime()), '11/7/2026 12:30:00 PM');
    });

    it('returns unparsable input unchanged', () => {
        assert.equal(formatDataMinerDateTime(' not a date '), 'not a date');
    });
});

describe('getDataMinerTimestamp', () => {
    it('returns 0 for missing or unparsable values', () => {
        for (const value of [null, undefined, '', 'not a date']) assert.equal(getDataMinerTimestamp(value), 0);
    });

    it('passes epoch milliseconds through', () => {
        assert.equal(getDataMinerTimestamp('1793750400000'), 1793750400000);
    });

    it('parses ISO date strings', () => {
        assert.equal(getDataMinerTimestamp('2026-11-07T00:00:00Z'), Date.UTC(2026, 10, 7));
    });
});
