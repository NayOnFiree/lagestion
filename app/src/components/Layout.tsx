import { NavLink, Outlet } from 'react-router'

const links = [
  { to: '/', label: 'Accueil' },
  { to: '/demo', label: 'Démo' },
  { to: '/statut', label: 'Statut' },
]

/**
 * Coque mobile-first : titre discret en haut, contenu scrollable, et
 * navigation en bas de l'écran — dans le pouce, pour un usage à une main.
 */
export function Layout() {
  return (
    <div className="flex min-h-dvh flex-col bg-slate-50 text-slate-900">
      <header className="sticky top-0 z-10 border-b border-slate-200 bg-white/90 px-4 py-3 backdrop-blur">
        <h1 className="text-base font-semibold tracking-tight">LaGestion</h1>
      </header>

      <main className="flex-1 px-4 pt-4 pb-[calc(var(--spacing-nav)+env(safe-area-inset-bottom))]">
        <Outlet />
      </main>

      <nav className="fixed inset-x-0 bottom-0 z-10 border-t border-slate-200 bg-white pb-[env(safe-area-inset-bottom)]">
        <ul className="mx-auto flex h-nav max-w-lg items-stretch">
          {links.map((link) => (
            <li key={link.to} className="flex-1">
              <NavLink
                to={link.to}
                end={link.to === '/'}
                className={({ isActive }) =>
                  [
                    'flex h-full w-full items-center justify-center text-sm font-medium',
                    isActive ? 'text-slate-900' : 'text-slate-400',
                  ].join(' ')
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
