import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Field } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { api, ApiError } from '@/lib/api'
import { formatDayLabel, formatSlot, weekdays } from '@/lib/labels'
import { cn } from '@/lib/utils'
import type { components } from '@/types/api'

type AvailabilitySlot = components['schemas']['AvailabilitySlot']
type RecurringResult = components['schemas']['RecurringResult']

const today = () => new Date().toISOString().slice(0, 10)
const inMonths = (count: number) => {
  const date = new Date()
  date.setMonth(date.getMonth() + count)
  return date.toISOString().slice(0, 10)
}

export function AvailabilitiesPage() {
  const queryClient = useQueryClient()
  const from = today()
  const to = inMonths(6)

  const { data, isPending } = useQuery({
    queryKey: ['availabilities', from, to],
    queryFn: () => api.get<AvailabilitySlot[]>(`/me/availabilities?from=${from}&to=${to}`),
  })

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['availabilities'] })
    await queryClient.invalidateQueries({ queryKey: ['calendar'] })
  }

  return (
    <section>
      <h2 className="text-title font-semibold">Mes disponibilités</h2>
      <p className="mt-1 text-base text-secondary">
        Déclarez quand vous êtes joignable. L'agence ne vous proposera que ces créneaux.
      </p>

      <DeclareForm onDeclared={refresh} />
      <RecurringForm onDeclared={refresh} />

      <h3 className="mt-8 text-strong font-medium">Déclarations à venir</h3>
      {isPending ? (
        <div className="mt-3 h-40 w-full rounded-card bg-surface" aria-busy="true" />
      ) : (
        <SlotList slots={data ?? []} onChanged={refresh} />
      )}
    </section>
  )
}

function DeclareForm({ onDeclared }: { onDeclared: () => Promise<void> }) {
  const [date, setDate] = useState(today())
  const [wholeDay, setWholeDay] = useState(true)
  const [startsAt, setStartsAt] = useState('09:00')
  const [endsAt, setEndsAt] = useState('18:00')
  const [status, setStatus] = useState('Available')
  const [error, setError] = useState<string | null>(null)

  const declare = useMutation({
    mutationFn: () =>
      api.post<AvailabilitySlot>('/me/availabilities', {
        date,
        startsAt: wholeDay ? null : `${startsAt}:00`,
        endsAt: wholeDay ? null : `${endsAt}:00`,
        status,
      }),
    onSuccess: async () => {
      setError(null)
      await onDeclared()
    },
    onError: (cause) =>
      setError(cause instanceof ApiError ? cause.message : "La déclaration n'a pas pu être enregistrée."),
  })

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault()
        declare.mutate()
      }}
      className="mt-6 flex flex-col gap-4"
    >
      <h3 className="text-strong font-medium">Un jour</h3>

      <Field id="date" label="Date">
        <Input id="date" type="date" value={date} onChange={(e) => setDate(e.target.value)} />
      </Field>

      <Field id="status" label="Je suis">
        <Select value={status} onValueChange={setStatus}>
          <SelectTrigger id="status">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="Available">disponible</SelectItem>
            <SelectItem value="Unavailable">indisponible</SelectItem>
          </SelectContent>
        </Select>
      </Field>

      <div className="flex gap-2">
        <Button
          type="button"
          variant={wholeDay ? 'default' : 'outline'}
          className="flex-1"
          onClick={() => setWholeDay(true)}
        >
          Journée entière
        </Button>
        <Button
          type="button"
          variant={wholeDay ? 'outline' : 'default'}
          className="flex-1"
          onClick={() => setWholeDay(false)}
        >
          Créneau
        </Button>
      </div>

      {!wholeDay && (
        <div className="flex gap-3">
          <Field id="startsAt" label="De" className="flex-1">
            <Input
              id="startsAt"
              type="time"
              value={startsAt}
              onChange={(e) => setStartsAt(e.target.value)}
            />
          </Field>
          <Field id="endsAt" label="À" className="flex-1">
            <Input id="endsAt" type="time" value={endsAt} onChange={(e) => setEndsAt(e.target.value)} />
          </Field>
        </div>
      )}

      {error && (
        <p role="alert" className="text-base text-danger">
          {error}
        </p>
      )}

      <Button type="submit" size="block" disabled={declare.isPending}>
        {declare.isPending ? 'Enregistrement…' : 'Déclarer'}
      </Button>
    </form>
  )
}

function RecurringForm({ onDeclared }: { onDeclared: () => Promise<void> }) {
  const [selected, setSelected] = useState<string[]>([])
  const [status, setStatus] = useState('Available')
  const [error, setError] = useState<string | null>(null)
  const [summary, setSummary] = useState<string | null>(null)

  const declare = useMutation({
    mutationFn: () =>
      api.post<RecurringResult>('/me/availabilities/recurring', {
        from: today(),
        to: inMonths(6),
        weekdays: selected,
        startsAt: null,
        endsAt: null,
        status,
      }),
    onSuccess: async (result) => {
      setError(null)
      setSummary(
        `${result.created.length} jour${result.created.length > 1 ? 's' : ''} déclaré${
          result.created.length > 1 ? 's' : ''
        }` +
          (result.skippedForConfirmedMission.length > 0
            ? `, ${result.skippedForConfirmedMission.length} laissé${
                result.skippedForConfirmedMission.length > 1 ? 's' : ''
              } de côté pour cause de mission confirmée`
            : '.'),
      )
      await onDeclared()
    },
    onError: (cause) =>
      setError(cause instanceof ApiError ? cause.message : "La déclaration n'a pas pu être enregistrée."),
  })

  const toggle = (value: string) =>
    setSelected((current) =>
      current.includes(value) ? current.filter((item) => item !== value) : [...current, value],
    )

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault()
        declare.mutate()
      }}
      className="mt-8 flex flex-col gap-4"
    >
      <div>
        <h3 className="text-strong font-medium">Tous les mêmes jours</h3>
        <p className="mt-1 text-base text-secondary">
          Applique la déclaration aux six prochains mois. Chaque jour reste modifiable ensuite.
        </p>
      </div>

      <div className="flex flex-wrap gap-2">
        {weekdays.map((day) => (
          <button
            key={day.value}
            type="button"
            onClick={() => toggle(day.value)}
            aria-pressed={selected.includes(day.value)}
            className={cn(
              'h-11 min-w-11 rounded-control border px-3 text-base',
              selected.includes(day.value)
                ? 'border-accent bg-accent-weak font-medium text-accent'
                : 'border-border text-secondary',
            )}
          >
            {day.short}
          </button>
        ))}
      </div>

      <Field id="recurringStatus" label="Je suis">
        <Select value={status} onValueChange={setStatus}>
          <SelectTrigger id="recurringStatus">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="Available">disponible</SelectItem>
            <SelectItem value="Unavailable">indisponible</SelectItem>
          </SelectContent>
        </Select>
      </Field>

      {error && (
        <p role="alert" className="text-base text-danger">
          {error}
        </p>
      )}

      {summary && !error && (
        <p role="status" className="text-base text-success">
          {summary}
        </p>
      )}

      <Button
        type="submit"
        size="block"
        variant="outline"
        disabled={declare.isPending || selected.length === 0}
      >
        {declare.isPending ? 'Enregistrement…' : 'Appliquer sur six mois'}
      </Button>
    </form>
  )
}

function SlotList({
  slots,
  onChanged,
}: {
  slots: AvailabilitySlot[]
  onChanged: () => Promise<void>
}) {
  if (slots.length === 0) {
    return (
      <div className="mt-3">
        <p className="text-strong font-medium">Aucune disponibilité déclarée</p>
        <p className="mt-1 text-base text-secondary">
          Tant que rien n'est déclaré, l'agence ne sait pas quand vous solliciter.
        </p>
      </div>
    )
  }

  const byDate = slots.reduce<Record<string, AvailabilitySlot[]>>((groups, slot) => {
    ;(groups[slot.date] ??= []).push(slot)
    return groups
  }, {})

  return (
    <ul className="mt-3 flex flex-col gap-2">
      {Object.entries(byDate).map(([date, daySlots]) => (
        <li key={date}>
          <Card>
            <p className="text-strong font-medium first-letter:uppercase">{formatDayLabel(date)}</p>
            {daySlots.map((slot) => (
              <SlotRow key={slot.id} slot={slot} onChanged={onChanged} />
            ))}
          </Card>
        </li>
      ))}
    </ul>
  )
}

function SlotRow({ slot, onChanged }: { slot: AvailabilitySlot; onChanged: () => Promise<void> }) {
  const remove = useMutation({
    mutationFn: () => api.delete<void>(`/me/availabilities/${slot.id}`),
    onSuccess: onChanged,
  })

  return (
    <div className="mt-2 flex items-center justify-between gap-3">
      <p className="text-base">
        <span className={slot.status === 'Available' ? 'text-accent' : 'text-secondary'}>
          {slot.status === 'Available' ? 'disponible' : 'indisponible'}
        </span>{' '}
        <span className="text-secondary">{formatSlot(slot.startsAt, slot.endsAt)}</span>
      </p>
      <Button type="button" variant="ghost" size="dense" onClick={() => remove.mutate()}>
        Retirer
      </Button>
    </div>
  )
}
