import type { ApiResult } from './types.js';

async function request<T>(url: string, init?: RequestInit): Promise<ApiResult<T>> {
	try {
		const res = await fetch(url, {
			...init,
			headers: { 'Content-Type': 'application/json', ...init?.headers }
		});
		if (!res.ok) {
			const text = await res.text().catch(() => '');
			let error = `HTTP ${res.status}`;
			try {
				const json = JSON.parse(text);
				if (json.error) error = json.error;
				else if (json.detail) error = json.detail;
			} catch {
				if (text) error = text;
			}
			return { success: false, statusCode: res.status, error };
		}
		const value = (await res.json()) as T;
		return { success: true, statusCode: res.status, value };
	} catch (err) {
		return {
			success: false,
			statusCode: 0,
			error: err instanceof Error ? err.message : 'Network error'
		};
	}
}

export function get<T>(url: string): Promise<ApiResult<T>> {
	return request<T>(url);
}

export function post<T>(url: string, body?: unknown): Promise<ApiResult<T>> {
	return request<T>(url, {
		method: 'POST',
		body: body !== undefined ? JSON.stringify(body) : undefined
	});
}

export async function getText(url: string): Promise<ApiResult<string>> {
	try {
		const res = await fetch(url);
		if (!res.ok) {
			return { success: false, statusCode: res.status, error: `HTTP ${res.status}` };
		}
		const value = await res.text();
		return { success: true, statusCode: res.status, value };
	} catch (err) {
		return {
			success: false,
			statusCode: 0,
			error: err instanceof Error ? err.message : 'Network error'
		};
	}
}
