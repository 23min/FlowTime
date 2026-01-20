import test from 'node:test';
import assert from 'node:assert/strict';
import { assertBinWindow } from '../src/guards.js';

test('assertBinWindow accepts valid range', () => {
  assert.doesNotThrow(() => {
    assertBinWindow(0, 10, 100);
  });
});

test('assertBinWindow rejects inverted range', () => {
  assert.throws(() => {
    assertBinWindow(10, 5, 100);
  }, /endBin must be >= startBin/);
});

test('assertBinWindow rejects oversized window', () => {
  assert.throws(() => {
    assertBinWindow(0, 1001, 1000);
  }, /exceeds maxBins/);
});
