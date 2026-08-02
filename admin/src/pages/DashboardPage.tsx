/** Route de démonstration, volontairement vide. */
export function DashboardPage() {
  return <EmptyState title="Tableau de bord" description="Écran de démonstration, sans contenu." />
}

/**
 * État vide : un titre, une phrase, éventuellement une action. Pas
 * d'illustration, pas d'emoji, pas de grande icône grise centrée.
 */
export function EmptyState({ title, description }: { title: string; description: string }) {
  return (
    <section>
      <h1 className="text-title font-semibold">{title}</h1>
      <p className="mt-1 text-base text-secondary">{description}</p>
    </section>
  )
}
