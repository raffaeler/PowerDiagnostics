import type { ApiResponse } from '@/types/api'
import { toastError } from '@/stores/useToastStore'

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

  async delete<T>(relativeUrl: string): Promise<ApiResponse<T>> {
    return request<T>('DELETE', relativeUrl)
  },

  /** Multipart file upload (used for .dmp files). */
  async upload<T>(relativeUrl: string, file: File): Promise<ApiResponse<T>> {
    try {
      const form = new FormData()
      form.append('file', file)
      const response = await fetch(relativeUrl, { method: 'POST', body: form })
      if (!response.ok) {
        const msg = `Upload failed: HTTP ${response.status}`
        toastError(msg)
        return { isError: true, result: msg }
      }
      const result = (await response.json()) as T
      return { isError: false, result }
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e)
      toastError(msg)
      return { isError: true, result: msg }
    }
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
      const message = `HTTP ${response.status}: ${response.statusText || 'request failed'}`
      toastError(message)
      return { isError: true, result: message }
    }

    const contentType = response.headers.get('content-type')
    const result = contentType ? ((await response.json()) as T) : ('' as unknown as T)
    return { isError: false, result }
  } catch (e) {
    const message = e instanceof Error ? e.message : String(e)
    toastError(message)
    return { isError: true, result: message }
  }
}
