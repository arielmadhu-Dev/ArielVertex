import { createContext, useContext, useEffect, useState, ReactNode, useCallback } from 'react'
import { api, tokenStore, setUnauthorizedHandler } from './api'
import type { CurrentUser } from './types'

interface AuthState {
  user: CurrentUser | null
  loading: boolean
  login: (email: string, password: string) => Promise<CurrentUser>
  adopt: (user: CurrentUser) => void
  logout: () => void
  has: (perm: string) => boolean
}

const AuthContext = createContext<AuthState>(null as any)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [loading, setLoading] = useState(true)

  const logout = useCallback(() => {
    tokenStore.clear()
    setUser(null)
  }, [])

  useEffect(() => {
    setUnauthorizedHandler(() => setUser(null))
    const t = tokenStore.get()
    if (!t) { setLoading(false); return }
    api.get<CurrentUser>('/auth/me')
      .then((r) => setUser(r.data))
      .catch(() => tokenStore.clear())
      .finally(() => setLoading(false))
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const { data } = await api.post<{ token: string; user: CurrentUser }>('/auth/login', { email, password })
    tokenStore.set(data.token)
    setUser(data.user)
    return data.user
  }, [])

  const adopt = useCallback((u: CurrentUser) => setUser(u), [])
  const has = useCallback((perm: string) => !!user?.permissions.includes(perm), [user])

  return (
    <AuthContext.Provider value={{ user, loading, login, adopt, logout, has }}>
      {children}
    </AuthContext.Provider>
  )
}

export const useAuth = () => useContext(AuthContext)
