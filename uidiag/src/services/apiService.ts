import type { ApiResponse } from '@/types/api'

/** Thin wrapper around `fetch` for the DiagnosticServer REST API.
 *  Uses relative URLs — the Vite dev proxy forwards them to the backend;
 *  in production (served from wwwroot) they are same-origin. */
export const apiService = {
  async get<T>(relativeUrl: string): Promise<ApiResponse<T>> {
    return request<T>('GET', relativeUrl)
  },

  async post<T>(relativeUrl: string, body?: unknown): Promise<ApiResponse<T>> {
    return request<T>('POST', relativeUrl, body)
  },
}

async function request<T>(
  verb: string,
  relativeUrl: string,
  body?: unknown,
): Promise<ApiResponse<T>> {
  try {
    const init: RequestInit = {
      method: verb,
      headers: { 'Content-Type': 'application/json' },
    }
    if (body !== undefined) {
      init.body = JSON.stringify(body)
    }

    const response = await fetch(relativeUrl, init)

    if (!response.ok) {
      const message = `Fetch failed with HTTP status ${response.status} ${response.statusText}`
      return { isError: true, result: message }
    }

    const contentType = response.headers.get('content-type')
    const result = contentType ? ((await response.json()) as T) : ('' as unknown as T)
    return { isError: false, result }
  } catch (e) {
    return { isError: true, result: e instanceof Error ? e.message : String(e) }
  }
}
