import { PanelLeftClose, PanelLeftOpen } from 'lucide-react'
import { useState } from 'react'
import { NavLink, Outlet } from 'react-router'
import { useAuth } from '@/lib/auth-context'
import { cn } from '@/lib/utils'

// La barre basse est plafonnée à quatre entrées : au-delà, elle n'est plus
// atteignable au pouce. Les barres haute et latérale ont la place, elles
// montrent tout. /statut reste accessible par son URL, c'est un écran de
// diagnostic.
const links = [
  { to: '/', label: 'Accueil', onBottomBar: true },
  { to: '/missions', label: 'Missions', onBottomBar: true },
  { to: '/heures', label: 'Heures', onBottomBar: true },
  { to: '/dispos', label: 'Dispos', onBottomBar: true },
  { to: '/factures', label: 'Factures', onBottomBar: false },
  { to: '/documents', label: 'Documents', onBottomBar: false },
  { to: '/profil', label: 'Profil', onBottomBar: false },
]

const bottomBarLinks = links.filter((link) => link.onBottomBar)

/**
 * Coque responsive.
 *
 * La navigation change de forme plutôt que de grossir : barre basse dans le
 * pouce en mobile, barre haute en tablette, colonne rétractable en desktop.
 * Le contenu est plafonné à 1120px et centré sur grand écran, pour ne pas
 * laisser une colonne étroite perdue au milieu de l'écran.
 */
export function Layout() {
  const { user, signOut } = useAuth()
  const [sidebarOpen, setSidebarOpen] = useState(true)

  return (
    <div className="min-h-dvh xl:flex">
      <DesktopSidebar open={sidebarOpen} onToggle={() => setSidebarOpen((value) => !value)} />

      <div className="flex min-h-dvh min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-10 border-b border-border bg-bg xl:hidden">
          <div className="mx-auto flex w-full max-w-content items-center justify-between gap-4 px-4 py-3">
            {/* En mobile, c'est le seul chemin vers le profil et les documents,
                que la barre basse ne peut pas porter. */}
            <NavLink to="/profil" className="min-w-0 md:pointer-events-none">
              <p className="truncate text-strong font-medium">
                {user?.firstName} {user?.lastName}
              </p>
              <p className="truncate text-meta text-secondary">{user?.agencyName}</p>
            </NavLink>

            {/* Tablette : la navigation remonte dans l'en-tête. */}
            <nav className="hidden md:block">
              <ul className="flex items-center gap-1">
                {links.map((link) => (
                  <li key={link.to}>
                    <NavItem to={link.to} className="h-9 px-3">
                      {link.label}
                    </NavItem>
                  </li>
                ))}
              </ul>
            </nav>

            <button
              type="button"
              onClick={() => void signOut()}
              className="shrink-0 rounded-control px-3 py-2 text-base text-secondary hover:text-primary"
            >
              Déconnexion
            </button>
          </div>
        </header>

        <main className="mx-auto w-full max-w-content flex-1 px-4 pt-4 pb-[calc(var(--spacing-nav)+env(safe-area-inset-bottom)+16px)] md:pb-8 xl:px-8">
          <Outlet />
        </main>
      </div>

      {/* Mobile : barre basse fixe. */}
      <nav className="fixed inset-x-0 bottom-0 z-10 border-t border-border bg-bg pb-[env(safe-area-inset-bottom)] md:hidden">
        <ul className="mx-auto flex h-nav max-w-lg items-stretch">
          {bottomBarLinks.map((link) => (
            <li key={link.to} className="flex-1">
              <NavItem to={link.to} className="h-full w-full">
                {link.label}
              </NavItem>
            </li>
          ))}
        </ul>
      </nav>
    </div>
  )
}

function DesktopSidebar({ open, onToggle }: { open: boolean; onToggle: () => void }) {
  const { user, signOut } = useAuth()

  return (
    <aside
      className={cn(
        'hidden shrink-0 border-r border-border bg-bg xl:flex xl:flex-col',
        open ? 'w-sidebar' : 'w-16',
      )}
    >
      <div className="flex items-center justify-between gap-2 border-b border-border px-3 py-3">
        {open && (
          <div className="min-w-0">
            <p className="truncate text-strong font-medium">
              {user?.firstName} {user?.lastName}
            </p>
            <p className="truncate text-meta text-secondary">{user?.agencyName}</p>
          </div>
        )}
        <button
          type="button"
          onClick={onToggle}
          aria-label={open ? 'Réduire le menu' : 'Déployer le menu'}
          aria-expanded={open}
          className="shrink-0 rounded-control p-2 text-secondary hover:bg-surface hover:text-primary"
        >
          {open ? (
            <PanelLeftClose className="size-4" aria-hidden />
          ) : (
            <PanelLeftOpen className="size-4" aria-hidden />
          )}
        </button>
      </div>

      <nav className="flex-1 p-2">
        <ul className="flex flex-col gap-1">
          {links.map((link) => (
            <li key={link.to}>
              <NavItem
                to={link.to}
                title={open ? undefined : link.label}
                className={cn('h-9 w-full', open ? 'justify-start px-3' : 'justify-center px-0')}
              >
                {open ? link.label : link.label.slice(0, 1)}
              </NavItem>
            </li>
          ))}
        </ul>
      </nav>

      <div className="border-t border-border p-2">
        <button
          type="button"
          onClick={() => void signOut()}
          className={cn(
            'h-9 w-full rounded-control text-base text-secondary hover:bg-surface hover:text-primary',
            open ? 'px-3 text-left' : 'px-0',
          )}
        >
          {open ? 'Déconnexion' : '⏻'}
        </button>
      </div>
    </aside>
  )
}

function NavItem({
  to,
  className,
  children,
  title,
}: {
  to: string
  className?: string
  children: React.ReactNode
  title?: string
}) {
  return (
    <NavLink
      to={to}
      end={to === '/'}
      title={title}
      className={({ isActive }) =>
        cn(
          'flex items-center justify-center rounded-control text-base font-medium',
          isActive ? 'text-accent xl:bg-accent-weak' : 'text-secondary hover:text-primary',
          className,
        )
      }
    >
      {children}
    </NavLink>
  )
}
