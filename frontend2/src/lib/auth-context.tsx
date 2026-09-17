/* eslint-disable react-refresh/only-export-components */
import { createContext, useContext, useMemo, useState, type ReactNode } from 'react'
import { readAuth, writeAuth } from '@/lib/auth-storage'
import type { AuthUser, MenuItem, StoredAuth } from '@/lib/types'

interface AuthContextValue {
  user: AuthUser | null
  menuList: MenuItem[]
  isAuthenticated: boolean
  signIn: (payload: StoredAuth) => void
  signOut: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [auth, setAuth] = useState<StoredAuth | null>(() => readAuth())

  const value = useMemo<AuthContextValue>(
    () => ({
      user: auth?.user ?? null,
      menuList: auth?.menuList ?? [],
      isAuthenticated: Boolean(auth?.token),
      signIn: (payload) => {
        writeAuth(payload)
        setAuth(payload)
      },
      signOut: () => {
        writeAuth(null)
        setAuth(null)
      },
    }),
    [auth],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return ctx
}
