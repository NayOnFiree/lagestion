import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Field } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { api, ApiError } from '@/lib/api'
import { formatAmount, formatRange } from '@/lib/labels'
import { cn } from '@/lib/utils'
import type { components } from '@/types/api'

type Mission = components['schemas']['Mission']
type TimesheetView = components['schemas']['TimesheetView']

const statusLabels: Record<string, string> = {
  Submitted: 'en attente de validation',
  Validated: 'validées',
  Disputed: 'contestées',
}

/** Heures prévues d'après le créneau, pour préremplir la déclaration. */
function plannedHours(mission: Mission) {
  return mission.plannedHours
}

export function HoursPage() {
  const { data: past, isPending } = useQuery({
    queryKey: ['missions', 'past'],
    queryFn: () => api.get<Mission[]>('/me/missions?scope=past'),
  })

  const { data: sheets } = useQuery({
    queryKey: ['hours'],
    queryFn: () => api.get<TimesheetView[]>('/me/hours'),
  })

  // Un relevé contesté revient dans « à déclarer » : sans ça, l'invitation à
  // redéclarer n'aurait nulle part où mener.
  const settled = new Set(
    sheets?.filter((sheet) => sheet.status !== 'Disputed').map((sheet) => sheet.assignmentId),
  )
  const disputed = new Map(
    sheets?.filter((sheet) => sheet.status === 'Disputed').map((sheet) => [sheet.assignmentId, sheet]),
  )

  const toDeclare = past?.filter(
    (mission) => mission.status === 'Confirmed' && !settled.has(mission.id),
  )

  return (
    <section>
      <h2 className="text-title font-semibold">Mes heures</h2>
      <p className="mt-1 text-base text-secondary">
        Déclarez ce que vous avez réellement effectué. L'agence valide avant facturation.
      </p>

      {isPending ? (
        <div className="mt-4 h-40 w-full rounded-card bg-surface" aria-busy="true" />
      ) : (
        <>
          <h3 className="mt-6 text-strong font-medium">À déclarer</h3>
          {toDeclare && toDeclare.length > 0 ? (
            <ul className="mt-3 flex flex-col gap-3 xl:grid xl:grid-cols-2">
              {toDeclare.map((mission) => (
                <li key={mission.id}>
                  <DeclareCard mission={mission} disputed={disputed.get(mission.id)} />
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-1 text-base text-secondary">
              Rien à déclarer. Vos prestations terminées sont toutes renseignées.
            </p>
          )}

          <h3 className="mt-8 text-strong font-medium">Déclarées</h3>
          {sheets && sheets.length > 0 ? (
            <ul className="mt-3 flex flex-col gap-3 xl:grid xl:grid-cols-2">
              {sheets.map((sheet) => (
                <li key={sheet.id}>
                  <SheetCard sheet={sheet} />
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-1 text-base text-secondary">Aucune déclaration pour le moment.</p>
          )}
        </>
      )}
    </section>
  )
}

function DeclareCard({ mission, disputed }: { mission: Mission; disputed?: TimesheetView }) {
  const queryClient = useQueryClient()
  const [hours, setHours] = useState(String(disputed?.actualHours ?? plannedHours(mission)))
  const [note, setNote] = useState(disputed?.contractorNote ?? '')
  const [error, setError] = useState<string | null>(null)

  const declare = useMutation({
    mutationFn: () =>
      api.post<TimesheetView>(`/me/hours/${mission.id}`, {
        actualHours: Number(hours.replace(',', '.')),
        note: note || null,
      }),
    onSuccess: async () => {
      setError(null)
      await queryClient.invalidateQueries({ queryKey: ['hours'] })
    },
    onError: (cause) =>
      setError(cause instanceof ApiError ? cause.message : "La déclaration a échoué."),
  })

  return (
    <Card>
      <p className="text-strong font-medium">{mission.positionLabel}</p>
      <p className="mt-0.5 text-base text-secondary">{mission.eventTitle}</p>
      <p className="mt-1 text-base tabular-nums first-letter:uppercase">
        {formatRange(mission.startsAt, mission.endsAt)}
      </p>
      <p className="mt-1 text-meta text-secondary tabular-nums">
        prévu {mission.plannedHours} h à {formatAmount(mission.hourlyRate)} / h
      </p>

      {disputed?.reviewNote && (
        <p className="mt-3 text-base text-danger">
          L'agence conteste : {disputed.reviewNote}
        </p>
      )}

      <form
        onSubmit={(event) => {
          event.preventDefault()
          declare.mutate()
        }}
        className="mt-4 flex flex-col gap-3"
      >
        <Field id={`hours-${mission.id}`} label="Heures effectuées">
          <Input
            id={`hours-${mission.id}`}
            inputMode="decimal"
            value={hours}
            onChange={(event) => setHours(event.target.value)}
          />
        </Field>

        <Field
          id={`note-${mission.id}`}
          label="Commentaire"
          hint="Utile si l'horaire réel s'écarte du prévu"
        >
          <Input
            id={`note-${mission.id}`}
            value={note}
            onChange={(event) => setNote(event.target.value)}
          />
        </Field>

        {error && (
          <p role="alert" className="text-base text-danger">
            {error}
          </p>
        )}

        <Button type="submit" size="block" disabled={declare.isPending} className="xl:w-auto xl:self-end">
          {declare.isPending ? 'Envoi…' : 'Déclarer'}
        </Button>
      </form>
    </Card>
  )
}

function SheetCard({ sheet }: { sheet: TimesheetView }) {
  const variance = sheet.variance

  return (
    <Card>
      <div className="flex items-baseline justify-between gap-3">
        <p className="text-strong font-medium">{sheet.positionLabel}</p>
        <p
          className={cn(
            'text-meta font-medium',
            sheet.status === 'Validated'
              ? 'text-accent'
              : sheet.status === 'Disputed'
                ? 'text-danger'
                : 'text-secondary',
          )}
        >
          {statusLabels[sheet.status] ?? sheet.status}
        </p>
      </div>
      <p className="mt-0.5 text-base text-secondary">{sheet.eventTitle}</p>

      <dl className="mt-3 flex flex-col gap-2">
        <Row label="Prévu" value={`${sheet.plannedHours} h`} />
        <Row label="Déclaré" value={`${sheet.actualHours} h`} />
        <Row
          label="Écart"
          value={variance === 0 ? 'aucun' : `${variance > 0 ? '+' : ''}${variance} h`}
        />
        <Row label="Rémunération" value={formatAmount(sheet.amount)} />
      </dl>

      {sheet.reviewNote && (
        <p className={cn('mt-3 text-base', sheet.status === 'Disputed' ? 'text-danger' : 'text-secondary')}>
          Agence : {sheet.reviewNote}
        </p>
      )}

      {sheet.status === 'Disputed' && (
        <p className="mt-2 text-base text-secondary">
          Redéclarez vos heures depuis « à déclarer » si vous êtes d'accord, ou contactez l'agence.
        </p>
      )}
    </Card>
  )
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline justify-between gap-4">
      <dt className="text-base text-secondary">{label}</dt>
      <dd className="text-base font-medium tabular-nums">{value}</dd>
    </div>
  )
}
