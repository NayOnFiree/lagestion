import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { api, ApiError } from '@/lib/api'
import { formatAmount, formatDayLabel, formatTime } from '@/lib/labels'
import { cn } from '@/lib/utils'
import type { components } from '@/types/api'

type Mission = components['schemas']['Mission']

const tabs = [
  { scope: 'proposals', label: 'Propositions' },
  { scope: 'upcoming', label: 'À venir' },
  { scope: 'past', label: 'Passées' },
] as const

const statusLabels: Record<string, string> = {
  Proposed: 'à répondre',
  Accepted: 'en attente de confirmation',
  Confirmed: 'confirmée',
  Declined: 'refusée',
}

export function MissionsPage() {
  const [scope, setScope] = useState<(typeof tabs)[number]['scope']>('proposals')
  const [selectedId, setSelectedId] = useState<string | null>(null)

  const { data, isPending } = useQuery({
    queryKey: ['missions', scope],
    queryFn: () => api.get<Mission[]>(`/me/missions?scope=${scope}`),
  })

  const selected = data?.find((mission) => mission.id === selectedId) ?? data?.[0] ?? null

  return (
    <section>
      <h2 className="text-title font-semibold">Mes missions</h2>

      <div className="mt-4 flex gap-1" role="tablist">
        {tabs.map((tab) => (
          <button
            key={tab.scope}
            type="button"
            role="tab"
            aria-selected={scope === tab.scope}
            onClick={() => {
              setScope(tab.scope)
              setSelectedId(null)
            }}
            className={cn(
              'h-11 rounded-control px-3 text-base font-medium xl:h-9',
              scope === tab.scope ? 'bg-accent-weak text-accent' : 'text-secondary',
            )}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {isPending ? (
        <div className="mt-4 h-48 w-full rounded-card bg-surface" aria-busy="true" />
      ) : data && data.length === 0 ? (
        <EmptyState scope={scope} />
      ) : (
        // Une colonne en mobile, liste à gauche et détail à droite en desktop.
        <div className="mt-4 gap-6 xl:grid xl:grid-cols-[380px_1fr] xl:items-start">
          <ul className="flex flex-col gap-3">
            {data?.map((mission) => (
              <li key={mission.id}>
                <MissionCard
                  mission={mission}
                  selected={selected?.id === mission.id}
                  onSelect={() => setSelectedId(mission.id)}
                />
              </li>
            ))}
          </ul>

          {selected && (
            <div className="mt-6 xl:mt-0">
              <MissionDetail mission={selected} />
            </div>
          )}
        </div>
      )}
    </section>
  )
}

function EmptyState({ scope }: { scope: string }) {
  const message =
    scope === 'proposals'
      ? "Aucune proposition en attente. Déclarez vos disponibilités pour que l'agence puisse vous solliciter."
      : scope === 'upcoming'
        ? 'Aucune mission à venir pour le moment.'
        : 'Aucune mission passée.'

  return (
    <div className="mt-4">
      <p className="text-strong font-medium">Rien ici</p>
      <p className="mt-1 text-base text-secondary">{message}</p>
    </div>
  )
}

function MissionCard({
  mission,
  selected,
  onSelect,
}: {
  mission: Mission
  selected: boolean
  onSelect: () => void
}) {
  return (
    <button type="button" onClick={onSelect} className="w-full text-left">
      <Card className={cn('transition-opacity duration-100', selected && 'border-accent')}>
        <p className="text-strong font-medium">{mission.positionLabel}</p>
        <p className="mt-0.5 text-base text-secondary">{mission.eventTitle}</p>
        <p className="mt-2 text-base tabular-nums first-letter:uppercase">
          {formatDayLabel(mission.startsAt.slice(0, 10))}, {formatTime(mission.startsAt.slice(11, 19))}
          –{formatTime(mission.endsAt.slice(11, 19))}
        </p>
        <p
          className={cn(
            'mt-1 text-meta font-medium',
            mission.status === 'Confirmed' ? 'text-accent' : 'text-secondary',
          )}
        >
          {mission.isExpired && mission.status === 'Proposed'
            ? 'délai de réponse dépassé'
            : (statusLabels[mission.status] ?? mission.status)}
        </p>
      </Card>
    </button>
  )
}

function MissionDetail({ mission }: { mission: Mission }) {
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)

  const respond = useMutation({
    mutationFn: (action: 'accept' | 'decline') =>
      api.post<Mission>(`/me/missions/${mission.id}/${action}`),
    onSuccess: async () => {
      setError(null)
      await queryClient.invalidateQueries({ queryKey: ['missions'] })
      await queryClient.invalidateQueries({ queryKey: ['calendar'] })
    },
    onError: (cause) =>
      setError(cause instanceof ApiError ? cause.message : "La réponse n'a pas pu être envoyée."),
  })

  const answerable = mission.status === 'Proposed' && !mission.isExpired

  return (
    <Card>
      <h3 className="text-strong font-medium">{mission.positionLabel}</h3>
      <p className="mt-0.5 text-base text-secondary">
        {mission.eventTitle}
        {mission.clientName ? ` — ${mission.clientName}` : ' — client non communiqué'}
      </p>

      <dl className="mt-4 flex flex-col gap-2">
        <Row
          label="Quand"
          value={`${formatDayLabel(mission.startsAt.slice(0, 10))}, ${formatTime(
            mission.startsAt.slice(11, 19),
          )}–${formatTime(mission.endsAt.slice(11, 19))}`}
        />
        <Row label="Où" value={mission.address ?? 'non communiqué'} />
        <Row label="Tarif horaire" value={`${formatAmount(mission.hourlyRate)} / h`} />
        <Row
          label="Estimation"
          value={`${mission.plannedHours} h — ${formatAmount(mission.estimatedAmount)}`}
        />
        {mission.dressCode && <Row label="Tenue" value={mission.dressCode} />}
        {mission.responseDeadline && mission.status === 'Proposed' && (
          <Row
            label="Réponse attendue"
            value={`avant le ${formatDayLabel(mission.responseDeadline.slice(0, 10))}`}
          />
        )}
      </dl>

      {mission.brief && (
        <div className="mt-4">
          <p className="text-meta font-medium text-secondary">Ce qu'il y a à faire</p>
          <p className="mt-1 text-base">{mission.brief}</p>
        </div>
      )}

      {mission.accessNotes ? (
        <div className="mt-4">
          <p className="text-meta font-medium text-secondary">Accès</p>
          <p className="mt-1 text-base">{mission.accessNotes}</p>
        </div>
      ) : (
        mission.status !== 'Declined' && (
          <p className="mt-4 text-meta text-secondary">
            Les modalités d'accès vous seront transmises à la confirmation.
          </p>
        )
      )}

      {error && (
        <p role="alert" className="mt-4 text-base text-danger">
          {error}
        </p>
      )}

      {mission.status === 'Accepted' && (
        <p className="mt-4 text-base text-secondary">
          Vous avez accepté. L'agence confirme sous peu — le créneau n'est pas encore réservé.
        </p>
      )}

      {mission.isExpired && mission.status === 'Proposed' && (
        <p className="mt-4 text-base text-warning">
          Le délai de réponse est dépassé. Contactez l'agence si vous êtes toujours disponible.
        </p>
      )}

      {answerable && (
        <div className="mt-6 flex flex-col gap-2 xl:flex-row-reverse">
          <Button
            type="button"
            size="block"
            onClick={() => respond.mutate('accept')}
            disabled={respond.isPending}
            className="xl:w-auto"
          >
            Accepter
          </Button>
          <Button
            type="button"
            variant="outline"
            size="block"
            onClick={() => respond.mutate('decline')}
            disabled={respond.isPending}
            className="xl:w-auto"
          >
            Refuser
          </Button>
        </div>
      )}
    </Card>
  )
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline justify-between gap-4">
      <dt className="text-base text-secondary">{label}</dt>
      <dd className="text-right text-base font-medium tabular-nums first-letter:uppercase">
        {value}
      </dd>
    </div>
  )
}
