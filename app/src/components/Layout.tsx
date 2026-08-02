import { NavLink, Outlet } from 'react-router'
import { useAuth } from '@/lib/auth-context'
import { cn } from '@/lib/utils'

const links = [
  { to: '/', label: 'Accueil' },
  { to: '/demo', label: 'Démo' },
  { to: '/statut', label: 'Statut' },
]

/**
 * Coque mobile-first : en-tête discret, contenu scrollable, navigation basse
 * dans le pouce, pour un usage à une main.
 */
export function Layout() {
  const { user, signOut } = useAuth()

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="sticky top-0 z-10 flex items-center justify-between border-b border-border bg-bg px-4 py-3">
        <div className="min-w-0">
          <p className="truncate text-strong font-medium">
            {user?.firstName} {user?.lastName}
          </p>
          <p className="truncate text-meta text-secondary">{user?.agencyName}</p>
        </div>
        <button
          type="button"
          onClick={() => void signOut()}
          className="shrink-0 rounded-control px-3 py-2 text-base text-secondary"
        >
          Déconnexion
        </button>
      </header>

      <main className="flex-1 px-4 pt-4 pb-[calc(var(--spacing-nav)+env(safe-area-inset-bottom)+16px)]">
        <Outlet />
      </main>

      <nav className="fixed inset-x-0 bottom-0 z-10 border-t border-border bg-bg pb-[env(safe-area-inset-bottom)]">
        <ul className="mx-auto flex h-nav max-w-lg items-stretch">
          {links.map((link) => (
            <li key={link.to} className="flex-1">
              <NavLink
                to={link.to}
                end={link.to === '/'}
                className={({ isActive }) =>
                  cn(
                    'flex h-full w-full items-center justify-center text-base font-medium',
                    isActive ? 'text-accent' : 'text-secondary',
                  )
                }
              >
                {link.label}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
    </div>
  )
}
