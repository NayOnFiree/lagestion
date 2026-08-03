import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { api } from '@/lib/api'
import {
  formatAmount,
  formatClock,
  formatDayLabel,
  formatDayOf,
  formatTime,
  weekdays,
} from '@/lib/labels'
import { cn } from '@/lib/utils'
import type { components } from '@/types/api'

type MonthCalendar = components['schemas']['MonthCalendar']
type CalendarDay = components['schemas']['CalendarDay']
type DocumentVault = components['schemas']['DocumentVault']

/** Décalage à appliquer pour que la grille commence un lundi. */
function leadingBlanks(firstDayIso: string) {
  const weekday = new Date(`${firstDayIso}T00:00:00`).getDay()
  return (weekday + 6) % 7
}

export function HomePage() {
  const [monthOffset, setMonthOffset] = useState(0)

  const month = (() => {
    const now = new Date()
    const target = new Date(now.getFullYear(), now.getMonth() + monthOffset, 1)
    return `${target.getFullYear()}-${String(target.getMonth() + 1).padStart(2, '0')}`
  })()

  const { data, isPending } = useQuery({
    queryKey: ['calendar', month],
    queryFn: () => api.get<MonthCalendar>(`/me/calendar?month=${month}`),
  })

  const { data: vault } = useQuery({
    queryKey: ['documents'],
    queryFn: () => api.get<DocumentVault>('/me/documents'),
  })

  const monthLabel = new Date(`${month}-01T00:00:00`).toLocaleDateString('fr-FR', {
    month: 'long',
    year: 'numeric',
  })

  const nextMission = data?.days
    .flatMap((day) => day.missions)
    .find((mission) => new Date(mission.startsAt) >= new Date())

  return (
    <section>
      {vault && !vault.completeness.isComplete && <DossierAlert />}

      <header className="flex items-baseline justify-between gap-3">
        <h2 className="text-title font-semibold first-letter:uppercase">{monthLabel}</h2>
        <div className="flex gap-1">
          <Button
            type="button"
            variant="ghost"
            size="dense"
            onClick={() => setMonthOffset((value) => value - 1)}
            aria-label="Mois précédent"
          >
            ‹
          </Button>
          <Button
            type="button"
            variant="ghost"
            size="dense"
            onClick={() => setMonthOffset((value) => value + 1)}
            aria-label="Mois suivant"
          >
            ›
          </Button>
        </div>
      </header>

      {isPending ? <CalendarSkeleton /> : data && <Totals totals={data.totals} />}

      {nextMission && (
        <Card className="mt-4">
          <p className="text-meta font-medium text-secondary">Prochaine mission</p>
          <p className="mt-1 text-strong font-medium">{nextMission.positionLabel}</p>
          <p className="mt-0.5 text-base text-secondary">{nextMission.eventTitle}</p>
          <p className="mt-1 text-base">
            {formatDayOf(nextMission.startsAt)}, {formatClock(nextMission.startsAt)}
            {' – '}
            {formatClock(nextMission.endsAt)}
          </p>
        </Card>
      )}

      {data && <MonthGrid days={data.days} />}

      <Button asChild variant="outline" size="block" className="mt-6">
        <Link to="/dispos">Déclarer mes disponibilités</Link>
      </Button>
    </section>
  )
}

function DossierAlert() {
  return (
    <Card className="mb-4 border-warning">
      <p className="text-strong font-medium text-warning">Dossier incomplet</p>
      <p className="mt-1 text-base text-secondary">
        Il manque des pièces. L'agence ne peut pas vous proposer de mission tant que votre dossier
        n'est pas complet.
      </p>
      <Button asChild variant="outline" className="mt-3">
        <Link to="/documents">Compléter mon dossier</Link>
      </Button>
    </Card>
  )
}

function Totals({ totals }: { totals: components['schemas']['MonthTotals'] }) {
  return (
    <div className="mt-4 flex items-end gap-8">
      <div>
        <p className="text-hero font-semibold tabular-nums">{totals.plannedHours}</p>
        <p className="text-meta font-medium text-secondary">heures prévues</p>
      </div>
      <div>
        <p className="text-hero font-semibold tabular-nums">{formatAmount(totals.estimatedAmount)}</p>
        <p className="text-meta font-medium text-secondary">rémunération estimée</p>
      </div>
    </div>
  )
}

function MonthGrid({ days }: { days: CalendarDay[] }) {
  const [selected, setSelected] = useState<CalendarDay | null>(null)

  if (days.length === 0) {
    return null
  }

  return (
    <div className="mt-6">
      <div className="grid grid-cols-7 gap-1">
        {weekdays.map((day) => (
          <div key={day.value} className="pb-1 text-center text-meta font-medium text-secondary">
            {day.short.slice(0, 1).toUpperCase()}
          </div>
        ))}

        {Array.from({ length: leadingBlanks(days[0].date) }, (_, index) => (
          <div key={`blank-${index}`} />
        ))}

        {days.map((day) => (
          <button
            key={day.date}
            type="button"
            onClick={() => setSelected(day)}
            aria-pressed={selected?.date === day.date}
            className={cn(
              'flex h-11 items-center justify-center rounded-control text-base tabular-nums',
              day.state === 'confirmed' && 'bg-accent font-medium text-accent-contrast',
              day.state === 'available' && 'bg-accent-weak font-medium text-accent',
              day.state === 'unavailable' && 'bg-surface text-muted',
              day.state === 'none' && 'text-secondary',
              selected?.date === day.date && 'ring-2 ring-accent',
            )}
          >
            {Number(day.date.slice(8, 10))}
          </button>
        ))}
      </div>

      <ul className="mt-3 flex flex-wrap gap-x-4 gap-y-1">
        <Legend className="bg-accent" label="mission confirmée" />
        <Legend className="bg-accent-weak" label="disponible" />
        <Legend className="bg-surface" label="indisponible" />
      </ul>

      {selected && <DayDetail day={selected} />}
    </div>
  )
}

function Legend({ className, label }: { className: string; label: string }) {
  return (
    <li className="flex items-center gap-1.5 text-meta text-secondary">
      <span className={cn('inline-block size-3 rounded-full', className)} aria-hidden />
      {label}
    </li>
  )
}

function DayDetail({ day }: { day: CalendarDay }) {
  return (
    <Card className="mt-4">
      <p className="text-strong font-medium first-letter:uppercase">{formatDayLabel(day.date)}</p>

      {day.missions.length === 0 && day.slots.length === 0 && (
        <p className="mt-1 text-base text-secondary">Rien de déclaré ce jour-là.</p>
      )}

      {day.missions.map((mission) => (
        <p key={mission.assignmentId} className="mt-2 text-base">
          <span className="font-medium">{mission.positionLabel}</span> — {mission.eventTitle},{' '}
          {formatClock(mission.startsAt)}–{formatClock(mission.endsAt)}
        </p>
      ))}

      {day.slots.map((slot) => (
        <p key={slot.id} className="mt-2 text-base text-secondary">
          {slot.status === 'Available' ? 'disponible' : 'indisponible'}
          {slot.startsAt && slot.endsAt
            ? ` ${formatTime(slot.startsAt)}–${formatTime(slot.endsAt)}`
            : ' toute la journée'}
        </p>
      ))}
    </Card>
  )
}

function CalendarSkeleton() {
  return (
    <div aria-busy="true">
      <div className="mt-4 h-12 w-48 rounded-control bg-surface" />
      <div className="mt-6 h-64 w-full rounded-card bg-surface" />
    </div>
  )
}
