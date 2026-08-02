import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, refreshSession, setAccessToken, setSessionLostHandler } from '@/lib/api'
import {
  AuthContext,
  type AuthState,
  type AuthenticatedUser,
  type LoginRequest,
} from '@/lib/auth-context'
import type { components } from '@/types/api'

type AuthResponse = components['schemas']['AuthResponse']

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthenticatedUser | null>(null)
  const [isRestoring, setIsRestoring] = useState(true)

  const clearSession = useCallback(() => {
    setAccessToken(null)
    setUser(null)
  }, [])

  // Au chargement, l'access token est perdu : il vivait en mémoire. Le cookie
  // de rafraîchissement, lui, a survécu — on tente de rétablir la session.
  //
  // Passe par refreshSession() et non par api.post directement : le jeton est
  // à usage unique, et StrictMode exécute cet effet deux fois. Deux requêtes
  // concurrentes présenteraient le même jeton, et le serveur prendrait la
  // seconde pour un rejeu.
  useEffect(() => {
    let cancelled = false

    const restore = async () => {
      const session = (await refreshSession()) as AuthResponse | null

      if (cancelled) {
        return
      }

      if (session) {
        setUser(session.user)
      } else {
        clearSession()
      }

      setIsRestoring(false)
    }

    void restore()

    return () => {
      cancelled = true
    }
  }, [clearSession])

  // Quand le client HTTP n'arrive plus à rafraîchir, la session est perdue.
  useEffect(() => {
    setSessionLostHandler(clearSession)
    return () => setSessionLostHandler(null)
  }, [clearSession])

  const signIn = useCallback(async (credentials: LoginRequest) => {
    const session = await api.post<AuthResponse>('/auth/login', credentials)
    setAccessToken(session.accessToken)
    setUser(session.user)
  }, [])

  const signOut = useCallback(async () => {
    try {
      await api.post('/auth/logout')
    } finally {
      clearSession()
    }
  }, [clearSession])

  const value = useMemo<AuthState>(
    () => ({ user, isRestoring, signIn, signOut }),
    [user, isRestoring, signIn, signOut],
  )

  return <AuthContext value={value}>{children}</AuthContext>
}
