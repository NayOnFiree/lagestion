import { Navigate, Outlet, useLocation } from 'react-router'
import { useAuth } from '@/lib/auth-context'

/**
 * Barrière d'accès aux écrans authentifiés.
 *
 * Tant que la session est en cours de rétablissement, on n'affiche ni le
 * contenu ni l'écran de connexion : basculer sur « connectez-vous » pour
 * revenir aussitôt en arrière serait un clignotement à chaque rechargement.
 */
export function RequireAuth() {
  const { user, isRestoring } = useAuth()
  const location = useLocation()

  if (isRestoring) {
    return <RestoringSkeleton />
  }

  if (!user) {
    return <Navigate to="/connexion" replace state={{ from: location.pathname }} />
  }

  return <Outlet />
}

/** Squelette aux dimensions réelles de l'écran attendu, pas un spinner. */
function RestoringSkeleton() {
  return (
    <div className="flex min-h-dvh flex-col px-4 py-4" aria-busy="true">
      <div className="h-6 w-32 rounded-control bg-surface" />
      <div className="mt-6 h-24 w-full rounded-card bg-surface" />
      <div className="mt-3 h-24 w-full rounded-card bg-surface" />
    </div>
  )
}
