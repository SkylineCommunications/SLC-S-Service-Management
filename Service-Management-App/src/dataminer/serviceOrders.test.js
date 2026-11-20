import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { getOrderItemTransitions, getOrderTransitions, isOpenOrder } from './serviceOrders.js';

const labels = transitions => transitions.map(transition => transition.label);

describe('getOrderTransitions', () => {
    it('offers Acknowledge and Reject for a new order', () => {
        assert.deepEqual(labels(getOrderTransitions('New')), ['Acknowledge', 'Reject']);
    });

    it('matches the state case- and whitespace-insensitively', () => {
        assert.deepEqual(getOrderTransitions('  IN PROGRESS '), getOrderTransitions('In Progress'));
    });

    it('offers no transitions for final or unknown states', () => {
        for (const state of ['Completed', 'Cancelled', 'Rejected', 'Failed', 'Unknown', '', null, undefined]) {
            assert.deepEqual(getOrderTransitions(state), [], `state ${state}`);
        }
    });

    it('uses the same rules for order items', () => {
        for (const state of ['New', 'Acknowledged', 'In Progress', 'Pending', 'Held']) {
            assert.deepEqual(getOrderItemTransitions(state), getOrderTransitions(state), `state ${state}`);
        }
    });
});

describe('isOpenOrder', () => {
    it('treats completed, cancelled, rejected and failed orders as closed', () => {
        for (const state of ['Completed', 'cancelled', ' Rejected ', 'FAILED']) assert.equal(isOpenOrder({ state }), false, state);
    });

    it('treats other states as open', () => {
        for (const state of ['New', 'In Progress', 'Held', 'Partially Failed']) assert.equal(isOpenOrder({ state }), true, state);
    });
});
