import { NavLink, Outlet } from 'react-router'
import { useAuth } from '@/lib/auth-context'
import { cn } from '@/lib/utils'

const links = [
  { to: '/', label: 'Tableau de bord' },
  { to: '/evenements', label: 'Événements' },
  { to: '/heures', label: 'Heures' },
  { to: '/factures', label: 'Factures' },
  { to: '/reseau', label: 'Réseau' },
  { to: '/conformite', label: 'Conformité' },
  { to: '/envois', label: 'Envois' },
  { to: '/statut', label: 'Statut API' },
]

/**
 * Coque desktop : sidebar fixe à gauche, contenu pleine largeur prévu pour
 * des tableaux denses.
 */
export function Layout() {
  const { user, signOut } = useAuth()

  return (
    <div className="flex min-h-dvh">
      <aside className="flex w-sidebar shrink-0 flex-col border-r border-border">
        <div className="border-b border-border px-4 py-3">
          <span className="text-strong font-medium">LaGestion</span>
          <span className="ml-1.5 text-meta text-secondary">admin</span>
        </div>

        <nav className="flex-1 p-2">
          <ul className="flex flex-col gap-0.5">
            {links.map((link) => (
              <li key={link.to}>
                <NavLink
                  to={link.to}
                  end={link.to === '/'}
                  className={({ isActive }) =>
                    cn(
                      'block rounded-control px-3 py-2 text-base',
                      isActive
                        ? 'bg-accent-weak font-medium text-accent'
                        : 'text-secondary hover:bg-surface hover:text-primary',
                    )
                  }
                >
                  {link.label}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>

        <div className="border-t border-border p-3">
          <p className="truncate text-base font-medium">
            {user?.firstName} {user?.lastName}
          </p>
          <p className="truncate text-meta text-secondary">{user?.agencyName}</p>
          <button
            type="button"
            onClick={() => void signOut()}
            className="mt-2 rounded-control text-base text-secondary hover:text-primary"
          >
            Déconnexion
          </button>
        </div>
      </aside>

      <main className="min-w-0 flex-1 p-6">
        <Outlet />
      </main>
    </div>
  )
}
