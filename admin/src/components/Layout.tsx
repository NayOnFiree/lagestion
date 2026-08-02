import { NavLink, Outlet } from 'react-router'

const links = [
  { to: '/', label: 'Tableau de bord' },
  { to: '/demo', label: 'Démo' },
  { to: '/statut', label: 'Statut API' },
]

/**
 * Coque desktop : sidebar fixe à gauche, zone de contenu pleine largeur
 * prévue pour des tableaux denses.
 */
export function Layout() {
  return (
    <div className="flex min-h-dvh bg-slate-100 text-slate-900">
      <aside className="w-sidebar shrink-0 border-r border-slate-200 bg-white">
        <div className="border-b border-slate-200 px-4 py-3">
          <span className="text-sm font-semibold tracking-tight">LaGestion</span>
          <span className="ml-1.5 text-xs text-slate-400">admin</span>
        </div>

        <nav className="p-2">
          <ul className="space-y-0.5">
            {links.map((link) => (
              <li key={link.to}>
                <NavLink
                  to={link.to}
                  end={link.to === '/'}
                  className={({ isActive }) =>
                    [
                      'block rounded px-2.5 py-1.5 text-sm',
                      isActive
                        ? 'bg-slate-900 text-white'
                        : 'text-slate-600 hover:bg-slate-100',
                    ].join(' ')
                  }
                >
                  {link.label}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
      </aside>

      <main className="min-w-0 flex-1 p-6">
        <Outlet />
      </main>
    </div>
  )
}
