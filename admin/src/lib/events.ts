/** Libellés et formats propres aux événements. */

export const eventStatusLabels: Record<string, string> = {
  Draft: 'brouillon',
  Published: 'publié',
  Cancelled: 'annulé',
}

export const eventStatusTones: Record<string, 'neutral' | 'accent' | 'danger'> = {
  Draft: 'neutral',
  Published: 'accent',
  Cancelled: 'danger',
}

/** « 15 oct. 2026, 7h » — jamais 15/10/2026 07:00. */
export function formatMoment(iso: string) {
  const date = new Date(iso)
  const day = date.toLocaleDateString('fr-FR', { day: 'numeric', month: 'short', year: 'numeric' })
  const time = date.toLocaleTimeString('fr-FR', { hour: 'numeric', minute: '2-digit' })

  return `${day}, ${time.replace(':', 'h').replace(/h00$/, 'h')}`
}

/** Période d'un événement, en évitant de répéter la date si elle est la même. */
export function formatRange(startsAt: string, endsAt: string) {
  const start = new Date(startsAt)
  const end = new Date(endsAt)

  if (start.toDateString() === end.toDateString()) {
    const time = end
      .toLocaleTimeString('fr-FR', { hour: 'numeric', minute: '2-digit' })
      .replace(':', 'h')
      .replace(/h00$/, 'h')

    return `${formatMoment(startsAt)} – ${time}`
  }

  return `${formatMoment(startsAt)} – ${formatMoment(endsAt)}`
}

/** Convertit un instant ISO en valeur d'un input datetime-local. */
export function toLocalInput(iso: string) {
  const date = new Date(iso)
  const offset = date.getTimezoneOffset()

  return new Date(date.getTime() - offset * 60_000).toISOString().slice(0, 16)
}

/** Convertit une valeur d'input datetime-local en instant ISO. */
export function fromLocalInput(value: string) {
  return new Date(value).toISOString()
}

export function formatRate(rate: number) {
  return `${rate.toLocaleString('fr-FR', { minimumFractionDigits: 2 })} €`
}
