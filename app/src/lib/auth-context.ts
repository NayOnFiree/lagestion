import { createContext, use } from 'react'
import type { components } from '@/types/api'

export type AuthenticatedUser = components['schemas']['AuthenticatedUser']
export type LoginRequest = components['schemas']['LoginRequest']

export interface AuthState {
  user: AuthenticatedUser | null
  /** Vrai tant que la session n'a pas été rétablie au chargement. */
  isRestoring: boolean
  signIn: (credentials: LoginRequest) => Promise<void>
  signOut: () => Promise<void>
}

export const AuthContext = createContext<AuthState | null>(null)

export function useAuth() {
  const context = use(AuthContext)

  if (!context) {
    throw new Error('useAuth doit être utilisé à l’intérieur d’un AuthProvider.')
  }

  return context
}
