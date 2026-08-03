import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { EventDialog } from '@/components/EventDialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { api } from '@/lib/api'
import { eventStatusLabels, eventStatusTones, formatRange } from '@/lib/events'
import type { components } from '@/types/api'

type EventSummary = components['schemas']['EventSummary']

export function EventsPage() {
  const [status, setStatus] = useState('all')
  const [creating, setCreating] = useState(false)

  const { data, isPending } = useQuery({
    queryKey: ['events', status],
    queryFn: () =>
      api.get<EventSummary[]>(`/events${status === 'all' ? '' : `?status=${status}`}`),
  })

  return (
    <section>
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-title font-semibold">Événements</h1>
          <p className="mt-1 text-base text-secondary">
            À venir et en cours. Les événements passés restent accessibles par filtre.
          </p>
        </div>
        <Button type="button" onClick={() => setCreating(true)}>
          Nouvel événement
        </Button>
      </div>

      {/* Barre de filtres unique, au-dessus du tableau. */}
      <div className="mt-4 flex items-center gap-3">
        <Select value={status} onValueChange={setStatus}>
          <SelectTrigger className="h-8 w-48" aria-label="Filtrer par statut">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">tous les statuts</SelectItem>
            <SelectItem value="Draft">brouillon</SelectItem>
            <SelectItem value="Published">publié</SelectItem>
            <SelectItem value="Cancelled">annulé</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {isPending ? (
        <div className="mt-4 h-64 w-full rounded-card bg-surface" aria-busy="true" />
      ) : (
        <EventsTable events={data ?? []} />
      )}

      {creating && <EventDialog open onOpenChange={() => setCreating(false)} />}
    </section>
  )
}

function EventsTable({ events }: { events: EventSummary[] }) {
  if (events.length === 0) {
    return (
      <div className="mt-4 rounded-card border border-border p-6">
        <p className="text-strong font-medium">Aucun événement</p>
        <p className="mt-1 text-base text-secondary">
          Créez un événement, puis découpez-le en postes pour pouvoir staffer.
        </p>
      </div>
    )
  }

  return (
    <div className="mt-4 overflow-x-auto rounded-card border border-border">
      <table className="w-full text-left text-dense">
        <thead className="sticky top-0 bg-surface text-meta font-medium text-secondary">
          <tr>
            <th className="h-row px-3">Intitulé</th>
            <th className="h-row px-3">Client</th>
            <th className="h-row px-3">Période</th>
            <th className="h-row px-3">Lieu</th>
            <th className="h-row px-3 text-right">Postes</th>
            <th className="h-row px-3 text-right">Pourvus</th>
            <th className="h-row px-3">Statut</th>
          </tr>
        </thead>
        <tbody>
          {events.map((event) => (
            <tr key={event.id} className="group border-t border-border hover:bg-surface">
              <td className="h-row px-3">
                <Link
                  to={`/evenements/${event.id}`}
                  className="font-medium outline-none hover:text-accent focus-visible:text-accent"
                >
                  {event.title}
                </Link>
              </td>
              <td className="h-row px-3 text-secondary">
                {event.isConfidential ? 'confidentiel' : (event.clientName ?? '—')}
              </td>
              <td className="h-row px-3 tabular-nums">{formatRange(event.startsAt, event.endsAt)}</td>
              <td className="h-row px-3 text-secondary">{event.address ?? '—'}</td>
              <td className="h-row px-3 text-right tabular-nums">{event.positionCount}</td>
              <td className="h-row px-3 text-right tabular-nums">
                <span className={event.filledCount < event.headcount ? 'text-warning' : undefined}>
                  {event.filledCount}/{event.headcount}
                </span>
              </td>
              <td className="h-row px-3">
                <Badge tone={eventStatusTones[event.status]}>
                  {eventStatusLabels[event.status] ?? event.status}
                </Badge>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
