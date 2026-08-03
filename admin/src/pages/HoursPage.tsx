import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { api, ApiError } from '@/lib/api'
import { formatRange, formatRate } from '@/lib/events'
import { cn } from '@/lib/utils'
import type { components } from '@/types/api'

type TimesheetView = components['schemas']['TimesheetView']
type MissingDeclaration = components['schemas']['MissingDeclaration']

const statusLabels: Record<string, string> = {
  Submitted: 'à valider',
  Validated: 'validées',
  Disputed: 'contestées',
}

export function HoursPage() {
  const { data: sheets, isPending } = useQuery({
    queryKey: ['timesheets'],
    queryFn: () => api.get<TimesheetView[]>('/timesheets'),
  })

  const { data: missing } = useQuery({
    queryKey: ['timesheets', 'missing'],
    queryFn: () => api.get<MissingDeclaration[]>('/timesheets/missing'),
  })

  return (
    <section>
      <h1 className="text-title font-semibold">Heures</h1>
      <p className="mt-1 text-base text-secondary">
        Écart entre prévu et déclaré, à valider avant facturation.
      </p>

      <h2 className="mt-6 text-strong font-medium">Relevés</h2>
      {isPending ? (
        <div className="mt-3 h-40 w-full rounded-card bg-surface" aria-busy="true" />
      ) : (
        <SheetsTable sheets={sheets ?? []} />
      )}

      <h2 className="mt-8 text-strong font-medium">Prestations sans déclaration</h2>
      <MissingTable rows={missing ?? []} />
    </section>
  )
}

function SheetsTable({ sheets }: { sheets: TimesheetView[] }) {
  if (sheets.length === 0) {
    return (
      <div className="mt-3 rounded-card border border-border p-6">
        <p className="text-strong font-medium">Aucun relevé</p>
        <p className="mt-1 text-base text-secondary">
          Les prestataires n'ont encore déclaré aucune heure.
        </p>
      </div>
    )
  }

  return (
    <div className="mt-3 overflow-x-auto rounded-card border border-border">
      <table className="w-full text-left text-dense">
        <thead className="sticky top-0 bg-surface text-meta font-medium text-secondary">
          <tr>
            <th className="h-row px-3">Prestataire</th>
            <th className="h-row px-3">Prestation</th>
            <th className="h-row px-3">Créneau</th>
            <th className="h-row px-3 text-right">Prévu</th>
            <th className="h-row px-3 text-right">Déclaré</th>
            <th className="h-row px-3 text-right">Écart</th>
            <th className="h-row px-3 text-right">Montant</th>
            <th className="h-row px-3">État</th>
            <th className="h-row px-3" />
          </tr>
        </thead>
        <tbody>
          {sheets.map((sheet) => (
            <SheetRow key={sheet.id} sheet={sheet} />
          ))}
        </tbody>
      </table>
    </div>
  )
}

function SheetRow({ sheet }: { sheet: TimesheetView }) {
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [hours, setHours] = useState(String(sheet.actualHours))
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['timesheets'] })
  }

  const review = useMutation({
    mutationFn: (validated: boolean) =>
      api.post<TimesheetView>(`/timesheets/${sheet.id}/review`, {
        validated,
        actualHours: Number(hours.replace(',', '.')) === sheet.actualHours
          ? null
          : Number(hours.replace(',', '.')),
        note: note || null,
      }),
    onSuccess: async () => {
      setError(null)
      setOpen(false)
      setNote('')
      await refresh()
    },
    onError: (cause) =>
      setError(cause instanceof ApiError ? cause.message : "L'opération a échoué."),
  })

  return (
    <>
      <tr className="group border-t border-border hover:bg-surface">
        <td className="h-row px-3 font-medium">{sheet.contractorName}</td>
        <td className="h-row px-3">
          {sheet.positionLabel}
          <span className="ml-2 text-secondary">{sheet.eventTitle}</span>
        </td>
        <td className="h-row px-3 tabular-nums text-secondary">
          {formatRange(sheet.startsAt, sheet.endsAt)}
        </td>
        <td className="h-row px-3 text-right tabular-nums">{sheet.plannedHours} h</td>
        <td className="h-row px-3 text-right tabular-nums">{sheet.actualHours} h</td>
        <td
          className={cn(
            'h-row px-3 text-right tabular-nums',
            sheet.variance !== 0 && 'text-warning',
          )}
        >
          {sheet.variance === 0 ? '—' : `${sheet.variance > 0 ? '+' : ''}${sheet.variance} h`}
        </td>
        <td className="h-row px-3 text-right tabular-nums">{formatRate(sheet.amount)}</td>
        <td className="h-row px-3">
          <Badge
            tone={
              sheet.status === 'Validated'
                ? 'accent'
                : sheet.status === 'Disputed'
                  ? 'danger'
                  : 'neutral'
            }
          >
            {statusLabels[sheet.status] ?? sheet.status}
          </Badge>
        </td>
        <td className="h-row px-3">
          {sheet.status !== 'Validated' && (
            <div className="flex justify-end opacity-0 group-hover:opacity-100 group-focus-within:opacity-100">
              <Button type="button" variant="ghost" size="dense" onClick={() => setOpen((v) => !v)}>
                Traiter
              </Button>
            </div>
          )}
        </td>
      </tr>

      {(open || error || sheet.contractorNote || sheet.reviewNote) && (
        <tr className="border-t border-border bg-surface">
          <td colSpan={9} className="px-3 py-3">
            {sheet.contractorNote && (
              <p className="text-base text-secondary">
                Prestataire : {sheet.contractorNote}
              </p>
            )}
            {sheet.reviewNote && (
              <p className="mt-1 text-base text-secondary">Agence : {sheet.reviewNote}</p>
            )}
            {error && (
              <p role="alert" className="mt-2 text-base text-danger">
                {error}
              </p>
            )}

            {open && (
              <div className="mt-3 flex items-center gap-2">
                <Input
                  value={hours}
                  onChange={(event) => setHours(event.target.value)}
                  aria-label="Heures retenues"
                  className="h-8 w-24"
                />
                <Input
                  value={note}
                  onChange={(event) => setNote(event.target.value)}
                  placeholder="Motif, obligatoire si vous corrigez ou contestez"
                  aria-label="Motif"
                  className="h-8 flex-1"
                />
                <Button type="button" size="dense" onClick={() => review.mutate(true)}>
                  Valider
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  size="dense"
                  onClick={() => review.mutate(false)}
                >
                  Contester
                </Button>
              </div>
            )}
          </td>
        </tr>
      )}
    </>
  )
}

function MissingTable({ rows }: { rows: MissingDeclaration[] }) {
  const queryClient = useQueryClient()
  const [entry, setEntry] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)

  const record = useMutation({
    mutationFn: (assignmentId: string) =>
      api.post<TimesheetView>('/timesheets/record', {
        assignmentId,
        actualHours: Number((entry[assignmentId] ?? '').replace(',', '.')),
        note: null,
      }),
    onSuccess: async () => {
      setError(null)
      await queryClient.invalidateQueries({ queryKey: ['timesheets'] })
    },
    onError: (cause) => setError(cause instanceof ApiError ? cause.message : 'La saisie a échoué.'),
  })

  if (rows.length === 0) {
    return (
      <div className="mt-3 rounded-card border border-border p-6">
        <p className="text-strong font-medium">Rien en attente</p>
        <p className="mt-1 text-base text-secondary">
          Toutes les prestations terminées ont un relevé.
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
              <th className="h-row px-3">Prestataire</th>
              <th className="h-row px-3">Prestation</th>
              <th className="h-row px-3">Créneau</th>
              <th className="h-row px-3 text-right">Prévu</th>
              <th className="h-row px-3">Heures retenues</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.assignmentId} className="border-t border-border hover:bg-surface">
                <td className="h-row px-3 font-medium">{row.contractorName}</td>
                <td className="h-row px-3">
                  {row.positionLabel}
                  <span className="ml-2 text-secondary">{row.eventTitle}</span>
                </td>
                <td className="h-row px-3 tabular-nums text-secondary">
                  {formatRange(row.startsAt, row.endsAt)}
                </td>
                <td className="h-row px-3 text-right tabular-nums">{row.plannedHours} h</td>
                <td className="h-row px-3">
                  <div className="flex items-center gap-2">
                    <Input
                      value={entry[row.assignmentId] ?? String(row.plannedHours)}
                      onChange={(event) =>
                        setEntry((current) => ({
                          ...current,
                          [row.assignmentId]: event.target.value,
                        }))
                      }
                      aria-label={`Heures pour ${row.contractorName}`}
                      className="h-8 w-24"
                    />
                    <Button
                      type="button"
                      size="dense"
                      variant="outline"
                      onClick={() => record.mutate(row.assignmentId)}
                    >
                      Saisir
                    </Button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}
