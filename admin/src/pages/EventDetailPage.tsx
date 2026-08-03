import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { EventDialog } from '@/components/EventDialog'
import { PositionDialog } from '@/components/PositionDialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent } from '@/components/ui/dialog'
import { Field } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { api, ApiError } from '@/lib/api'
import {
  eventStatusLabels,
  eventStatusTones,
  formatMoment,
  formatRange,
  formatRate,
  fromLocalInput,
  toLocalInput,
} from '@/lib/events'
import type { components } from '@/types/api'

type EventDetail = components['schemas']['EventDetail']
type PositionDetail = components['schemas']['PositionDetail']

export function EventDetailPage() {
  const { id = '' } = useParams()
  const queryClient = useQueryClient()

  const [editing, setEditing] = useState(false)
  const [duplicating, setDuplicating] = useState(false)
  const [addingPosition, setAddingPosition] = useState(false)
  const [editedPosition, setEditedPosition] = useState<PositionDetail | null>(null)
  const [error, setError] = useState<string | null>(null)

  const { data, isPending, isError } = useQuery({
    queryKey: ['event', id],
    queryFn: () => api.get<EventDetail>(`/events/${id}`),
  })

  const cancel = useMutation({
    mutationFn: () => api.post<EventDetail>(`/events/${id}/cancel`),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['event', id] })
      await queryClient.invalidateQueries({ queryKey: ['events'] })
    },
    onError: () => setError("L'annulation a échoué."),
  })

  if (isPending) {
    return <div className="h-64 w-full rounded-card bg-surface" aria-busy="true" />
  }

  if (isError || !data) {
    return (
      <section>
        <h1 className="text-title font-semibold">Événement introuvable</h1>
        <p className="mt-1 text-base text-secondary">
          Il a peut-être été supprimé, ou l'adresse est incorrecte.
        </p>
        <Button asChild variant="outline" className="mt-4">
          <Link to="/evenements">Retour aux événements</Link>
        </Button>
      </section>
    )
  }

  const cancelled = data.status === 'Cancelled'

  return (
    <section>
      <Link to="/evenements" className="text-meta text-secondary hover:text-primary">
        ← Événements
      </Link>

      <div className="mt-2 flex items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-title font-semibold">{data.title}</h1>
            <Badge tone={eventStatusTones[data.status]}>
              {eventStatusLabels[data.status] ?? data.status}
            </Badge>
          </div>
          <p className="mt-1 text-base text-secondary tabular-nums">
            {formatRange(data.startsAt, data.endsAt)}
            {data.address && ` — ${data.address}`}
          </p>
          <p className="mt-0.5 text-base text-secondary">
            {data.isConfidential
              ? 'client masqué aux prestataires'
              : (data.clientName ?? 'client non renseigné')}
          </p>
          {data.accessNotes && (
            <p className="mt-2 text-base">Accès : {data.accessNotes}</p>
          )}
          {cancelled && data.cancelledAt && (
            <p className="mt-2 text-base text-danger">
              Annulé le {formatMoment(data.cancelledAt)}. Les propositions en cours ont été annulées.
            </p>
          )}
        </div>

        <div className="flex shrink-0 gap-2">
          <Button type="button" variant="ghost" onClick={() => setDuplicating(true)}>
            Dupliquer
          </Button>
          {!cancelled && (
            <>
              <Button type="button" variant="ghost" onClick={() => cancel.mutate()}>
                Annuler l'événement
              </Button>
              <Button type="button" variant="outline" onClick={() => setEditing(true)}>
                Modifier
              </Button>
            </>
          )}
        </div>
      </div>

      {error && (
        <p role="alert" className="mt-3 text-base text-danger">
          {error}
        </p>
      )}

      <div className="mt-6 flex items-center justify-between gap-4">
        <h2 className="text-strong font-medium">Postes</h2>
        {!cancelled && (
          <Button type="button" onClick={() => setAddingPosition(true)}>
            Ajouter un poste
          </Button>
        )}
      </div>

      <PositionsTable
        positions={data.positions}
        readOnly={cancelled}
        onEdit={setEditedPosition}
        eventId={id}
      />

      {editing && <EventDialog open onOpenChange={() => setEditing(false)} existing={data} />}

      {addingPosition && (
        <PositionDialog
          open
          eventId={id}
          eventStartsAt={data.startsAt}
          onOpenChange={() => setAddingPosition(false)}
        />
      )}

      {editedPosition && (
        <PositionDialog
          open
          eventId={id}
          eventStartsAt={data.startsAt}
          existing={editedPosition}
          onOpenChange={() => setEditedPosition(null)}
        />
      )}

      {duplicating && (
        <DuplicateDialog event={data} onOpenChange={() => setDuplicating(false)} />
      )}
    </section>
  )
}

function PositionsTable({
  positions,
  readOnly,
  onEdit,
  eventId,
}: {
  positions: PositionDetail[]
  readOnly: boolean
  onEdit: (position: PositionDetail) => void
  eventId: string
}) {
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)

  const remove = useMutation({
    mutationFn: (positionId: string) => api.delete<void>(`/positions/${positionId}`),
    onSuccess: async () => {
      setError(null)
      await queryClient.invalidateQueries({ queryKey: ['event', eventId] })
      await queryClient.invalidateQueries({ queryKey: ['events'] })
    },
    onError: (cause) =>
      setError(cause instanceof ApiError ? cause.message : 'La suppression a échoué.'),
  })

  if (positions.length === 0) {
    return (
      <div className="mt-3 rounded-card border border-border p-6">
        <p className="text-strong font-medium">Aucun poste</p>
        <p className="mt-1 text-base text-secondary">
          Un événement sans poste ne peut pas être staffé. Ajoutez-en un.
        </p>
      </div>
    )
  }

  return (
    <>
      {error && (
        <p role="alert" className="mt-3 text-base text-danger">
          {error}
        </p>
      )}

      <div className="mt-3 overflow-x-auto rounded-card border border-border">
        <table className="w-full text-left text-dense">
          <thead className="sticky top-0 bg-surface text-meta font-medium text-secondary">
            <tr>
              <th className="h-row px-3">Poste</th>
              <th className="h-row px-3">Créneau</th>
              <th className="h-row px-3 text-right">Effectif</th>
              <th className="h-row px-3 text-right">Pourvus</th>
              <th className="h-row px-3 text-right">Tarif horaire</th>
              <th className="h-row px-3">Tenue</th>
              <th className="h-row px-3" />
            </tr>
          </thead>
          <tbody>
            {positions.map((position) => (
              <tr key={position.id} className="group border-t border-border hover:bg-surface">
                <td className="h-row px-3 font-medium">{position.label}</td>
                <td className="h-row px-3 tabular-nums">
                  {formatRange(position.startsAt, position.endsAt)}
                </td>
                <td className="h-row px-3 text-right tabular-nums">{position.headcount}</td>
                <td className="h-row px-3 text-right tabular-nums">
                  <span
                    className={position.filledCount < position.headcount ? 'text-warning' : undefined}
                  >
                    {position.filledCount}
                  </span>
                </td>
                <td className="h-row px-3 text-right tabular-nums">
                  {formatRate(position.hourlyRate)}
                </td>
                <td className="h-row px-3 text-secondary">{position.dressCode ?? '—'}</td>
                <td className="h-row px-3">
                  {!readOnly && (
                    // Actions au survol : cinq boutons visibles par ligne
                    // rendraient le tableau illisible.
                    <div className="flex justify-end gap-1 opacity-0 group-hover:opacity-100 group-focus-within:opacity-100">
                      <Button asChild size="dense" variant="outline">
                        <Link to={`/postes/${position.id}/staffing`}>Staffer</Link>
                      </Button>
                      <Button
                        type="button"
                        variant="ghost"
                        size="dense"
                        onClick={() => onEdit(position)}
                      >
                        Modifier
                      </Button>
                      <Button
                        type="button"
                        variant="ghost"
                        size="dense"
                        onClick={() => remove.mutate(position.id)}
                      >
                        Retirer
                      </Button>
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

function DuplicateDialog({
  event,
  onOpenChange,
}: {
  event: EventDetail
  onOpenChange: (open: boolean) => void
}) {
  const queryClient = useQueryClient()
  const [startsAt, setStartsAt] = useState(toLocalInput(event.startsAt))
  const [title, setTitle] = useState(`${event.title} (copie)`)
  const [error, setError] = useState<string | null>(null)

  const duplicate = useMutation({
    mutationFn: () =>
      api.post<EventDetail>(`/events/${event.id}/duplicate`, {
        startsAt: fromLocalInput(startsAt),
        title,
      }),
    onSuccess: async (copy) => {
      await queryClient.invalidateQueries({ queryKey: ['events'] })
      onOpenChange(false)
      window.location.assign(`/evenements/${copy.id}`)
    },
    onError: () => setError('La duplication a échoué.'),
  })

  return (
    <Dialog open onOpenChange={onOpenChange}>
      <DialogContent
        title="Dupliquer l'événement"
        description="Les postes sont recopiés et décalés d'autant. Les propositions de mission ne le sont jamais."
      >
        <form
          onSubmit={(submitEvent) => {
            submitEvent.preventDefault()
            duplicate.mutate()
          }}
          className="flex flex-col gap-4"
        >
          <Field id="copyTitle" label="Intitulé de la copie">
            <Input id="copyTitle" value={title} onChange={(e) => setTitle(e.target.value)} />
          </Field>

          <Field id="copyStartsAt" label="Nouveau début">
            <Input
              id="copyStartsAt"
              type="datetime-local"
              value={startsAt}
              onChange={(e) => setStartsAt(e.target.value)}
            />
          </Field>

          {error && (
            <p role="alert" className="text-base text-danger">
              {error}
            </p>
          )}

          <div className="mt-2 flex justify-end gap-2">
            <Button type="button" variant="ghost" onClick={() => onOpenChange(false)}>
              Annuler
            </Button>
            <Button type="submit" disabled={duplicate.isPending}>
              {duplicate.isPending ? 'Duplication…' : 'Dupliquer'}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}
