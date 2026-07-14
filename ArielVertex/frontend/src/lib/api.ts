import axios from 'axios'

const base = import.meta.env.VITE_API_BASE?.trim()
export const api = axios.create({
  baseURL: (base ? base.replace(/\/$/, '') : '') + '/api/v1',
})

const TOKEN_KEY = 'av-token'
export const tokenStore = {
  get: () => localStorage.getItem(TOKEN_KEY),
  set: (t: string) => localStorage.setItem(TOKEN_KEY, t),
  clear: () => localStorage.removeItem(TOKEN_KEY),
}

api.interceptors.request.use((config) => {
  const t = tokenStore.get()
  if (t) config.headers.Authorization = `Bearer ${t}`
  return config
})

// On 401 the token is stale/absent — bounce to login (route guard picks it up).
let onUnauthorized: (() => void) | null = null
export const setUnauthorizedHandler = (fn: () => void) => { onUnauthorized = fn }
api.interceptors.response.use(
  (r) => r,
  (err) => {
    if (err?.response?.status === 401) { tokenStore.clear(); onUnauthorized?.() }
    return Promise.reject(err)
  },
)

/** Extract a friendly message from our error envelope. */
export function apiError(err: unknown, fallback = 'Something went wrong.'): string {
  const e = err as any
  return e?.response?.data?.error?.message || e?.message || fallback
}
