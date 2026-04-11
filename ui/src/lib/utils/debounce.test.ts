import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { createDebouncer } from './debounce';

describe('createDebouncer', () => {
	beforeEach(() => {
		vi.useFakeTimers();
	});

	afterEach(() => {
		vi.useRealTimers();
	});

	it('does not call the function before the delay elapses', () => {
		const d = createDebouncer<number>(50);
		const fn = vi.fn();
		d.schedule(fn, 1);

		vi.advanceTimersByTime(49);
		expect(fn).not.toHaveBeenCalled();
	});

	it('calls the function after the delay elapses', () => {
		const d = createDebouncer<number>(50);
		const fn = vi.fn();
		d.schedule(fn, 42);

		vi.advanceTimersByTime(50);
		expect(fn).toHaveBeenCalledTimes(1);
		expect(fn).toHaveBeenCalledWith(42);
	});

	it('collapses multiple rapid calls to a single trailing-edge call', () => {
		const d = createDebouncer<number>(50);
		const fn = vi.fn();

		d.schedule(fn, 1);
		vi.advanceTimersByTime(10);
		d.schedule(fn, 2);
		vi.advanceTimersByTime(10);
		d.schedule(fn, 3);
		vi.advanceTimersByTime(10);
		d.schedule(fn, 4);

		expect(fn).not.toHaveBeenCalled();
		vi.advanceTimersByTime(50);
		expect(fn).toHaveBeenCalledTimes(1);
		expect(fn).toHaveBeenCalledWith(4);
	});

	it('flush runs the pending call immediately', () => {
		const d = createDebouncer<number>(50);
		const fn = vi.fn();

		d.schedule(fn, 99);
		d.flush();

		expect(fn).toHaveBeenCalledTimes(1);
		expect(fn).toHaveBeenCalledWith(99);

		// After flush, timer should not fire again
		vi.advanceTimersByTime(100);
		expect(fn).toHaveBeenCalledTimes(1);
	});

	it('flush is a no-op when nothing is pending', () => {
		const d = createDebouncer<number>(50);
		const fn = vi.fn();
		d.flush();
		expect(fn).not.toHaveBeenCalled();
	});

	it('cancel drops the pending call', () => {
		const d = createDebouncer<number>(50);
		const fn = vi.fn();

		d.schedule(fn, 1);
		d.cancel();
		vi.advanceTimersByTime(100);

		expect(fn).not.toHaveBeenCalled();
	});

	it('pending reflects timer state', () => {
		const d = createDebouncer<number>(50);
		const fn = vi.fn();

		expect(d.pending).toBe(false);
		d.schedule(fn, 1);
		expect(d.pending).toBe(true);
		vi.advanceTimersByTime(50);
		expect(d.pending).toBe(false);
	});

	it('can be reused after flush', () => {
		const d = createDebouncer<number>(50);
		const fn = vi.fn();

		d.schedule(fn, 1);
		d.flush();
		d.schedule(fn, 2);
		vi.advanceTimersByTime(50);

		expect(fn).toHaveBeenCalledTimes(2);
		expect(fn).toHaveBeenNthCalledWith(1, 1);
		expect(fn).toHaveBeenNthCalledWith(2, 2);
	});

	it('replaces fn and value on subsequent schedules', () => {
		const d = createDebouncer<string>(50);
		const fn1 = vi.fn();
		const fn2 = vi.fn();

		d.schedule(fn1, 'a');
		d.schedule(fn2, 'b');
		vi.advanceTimersByTime(50);

		expect(fn1).not.toHaveBeenCalled();
		expect(fn2).toHaveBeenCalledWith('b');
	});
});
