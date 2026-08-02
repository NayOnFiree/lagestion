/** Route de démonstration, volontairement vide. */
export function HomePage() {
  return <EmptyState title="Accueil" description="Écran de démonstration, sans contenu." />
}

/**
 * État vide : un titre, une phrase, éventuellement une action. Pas
 * d'illustration, pas d'emoji, pas de grande icône grise centrée.
 */
export function EmptyState({ title, description }: { title: string; description: string }) {
  return (
    <section>
      <h2 className="text-strong font-medium">{title}</h2>
      <p className="mt-1 text-base text-secondary">{description}</p>
    </section>
  )
}
