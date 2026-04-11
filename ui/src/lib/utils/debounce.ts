// Debounce utility: coalesces rapid calls into a single trailing-edge invocation.
//
// Usage:
//   const d = createDebouncer<number>(50);
//   d.schedule((v) => console.log(v), 1);  // does nothing yet
//   d.schedule((v) => console.log(v), 2);  // resets the timer
//   d.schedule((v) => console.log(v), 3);  // 50ms later: logs "3"
//
// flush() runs the pending call immediately (for drag-end to ensure final value).
// cancel() drops the pending call without running it.

export interface Debouncer<T> {
	/** Schedule a call. If another is pending, replace it and reset the timer. */
	schedule: (fn: (value: T) => void, value: T) => void;
	/** Immediately run the pending call, if any. */
	flush: () => void;
	/** Drop the pending call without running it. */
	cancel: () => void;
	/** True if a call is pending. */
	readonly pending: boolean;
}

export function createDebouncer<T>(delayMs: number): Debouncer<T> {
	let timerId: ReturnType<typeof setTimeout> | null = null;
	let pendingFn: ((value: T) => void) | null = null;
	let pendingValue: T | undefined = undefined;

	return {
		schedule(fn, value) {
			pendingFn = fn;
			pendingValue = value;
			if (timerId !== null) clearTimeout(timerId);
			timerId = setTimeout(() => {
				timerId = null;
				if (pendingFn) {
					const f = pendingFn;
					const v = pendingValue as T;
					pendingFn = null;
					pendingValue = undefined;
					f(v);
				}
			}, delayMs);
		},
		flush() {
			if (timerId !== null) {
				clearTimeout(timerId);
				timerId = null;
			}
			if (pendingFn) {
				const f = pendingFn;
				const v = pendingValue as T;
				pendingFn = null;
				pendingValue = undefined;
				f(v);
			}
		},
		cancel() {
			if (timerId !== null) {
				clearTimeout(timerId);
				timerId = null;
			}
			pendingFn = null;
			pendingValue = undefined;
		},
		get pending() {
			return timerId !== null;
		},
	};
}
