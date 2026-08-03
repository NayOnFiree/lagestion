import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { api, ApiError } from '@/lib/api'
import { formatAmount, formatDayOf } from '@/lib/labels'
import { cn } from '@/lib/utils'
import type { components } from '@/types/api'

type InvoiceDraft = components['schemas']['InvoiceDraft']
type InvoiceView = components['schemas']['InvoiceView']
type DocumentLink = components['schemas']['DocumentLink']

const statusLabels: Record<string, string> = {
  Issued: 'à déposer',
  Submitted: 'déposée',
  Validated: 'validée',
  Paid: 'payée',
  Cancelled: 'annulée',
}

/** Mois précédent, au format attendu par l'API. */
function previousMonth(offset: number) {
  const now = new Date()
  const target = new Date(now.getFullYear(), now.getMonth() - 1 - offset, 1)
  return `${target.getFullYear()}-${String(target.getMonth() + 1).padStart(2, '0')}`
}

export function InvoicesPage() {
  const [monthOffset, setMonthOffset] = useState(0)
  const month = previousMonth(monthOffset)

  const { data: draft, isPending } = useQuery({
    queryKey: ['invoice-draft', month],
    queryFn: () => api.get<InvoiceDraft>(`/me/invoices/draft?month=${month}`),
  })

  const { data: invoices } = useQuery({
    queryKey: ['invoices'],
    queryFn: () => api.get<InvoiceView[]>('/me/invoices'),
  })

  const monthLabel = new Date(`${month}-01T00:00:00`).toLocaleDateString('fr-FR', {
    month: 'long',
    year: 'numeric',
  })

  return (
    <section>
      <h2 className="text-title font-semibold">Mes factures</h2>
      <p className="mt-1 text-base text-secondary">
        Vous restez l'émetteur de vos factures. L'application les pré-remplit et génère le PDF,
        elle ne renumérote jamais.
      </p>

      <div className="mt-6 flex items-baseline justify-between gap-3">
        <h3 className="text-strong font-medium first-letter:uppercase">{monthLabel}</h3>
        <div className="flex gap-1">
          <Button
            type="button"
            variant="ghost"
            size="dense"
            onClick={() => setMonthOffset((value) => value + 1)}
            aria-label="Mois précédent"
          >
            ‹
          </Button>
          <Button
            type="button"
            variant="ghost"
            size="dense"
            onClick={() => setMonthOffset((value) => Math.max(0, value - 1))}
            disabled={monthOffset === 0}
            aria-label="Mois suivant"
          >
            ›
          </Button>
        </div>
      </div>

      {isPending ? (
        <div className="mt-3 h-40 w-full rounded-card bg-surface" aria-busy="true" />
      ) : (
        draft && <DraftCard draft={draft} />
      )}

      <h3 className="mt-8 text-strong font-medium">Factures émises</h3>
      {invoices && invoices.length > 0 ? (
        <ul className="mt-3 flex flex-col gap-3 xl:grid xl:grid-cols-2">
          {invoices.map((invoice) => (
            <li key={invoice.id}>
              <InvoiceCard invoice={invoice} />
            </li>
          ))}
        </ul>
      ) : (
        <p className="mt-1 text-base text-secondary">Aucune facture émise pour le moment.</p>
      )}
    </section>
  )
}

function DraftCard({ draft }: { draft: InvoiceDraft }) {
  const queryClient = useQueryClient()
  const [selected, setSelected] = useState<string[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  const chosen = selected ?? draft.missions.map((mission) => mission.timesheetId)
  const total = draft.missions
    .filter((mission) => chosen.includes(mission.timesheetId))
    .reduce((sum, mission) => sum + mission.amount, 0)

  const issue = useMutation({
    mutationFn: () =>
      api.post<InvoiceView>('/me/invoices', {
        periodStart: draft.periodStart,
        periodEnd: draft.periodEnd,
        timesheetIds: chosen,
      }),
    onSuccess: async () => {
      setError(null)
      setSelected(null)
      await queryClient.invalidateQueries({ queryKey: ['invoices'] })
      await queryClient.invalidateQueries({ queryKey: ['invoice-draft'] })
    },
    onError: (cause) => setError(cause instanceof ApiError ? cause.message : "L'émission a échoué."),
  })

  const toggle = (id: string) =>
    setSelected(chosen.includes(id) ? chosen.filter((item) => item !== id) : [...chosen, id])

  if (draft.missions.length === 0) {
    return (
      <Card className="mt-3">
        <p className="text-strong font-medium">Rien à facturer</p>
        <p className="mt-1 text-base text-secondary">
          Aucune prestation validée sur cette période. Les heures doivent être validées par
          l'agence avant de pouvoir être facturées.
        </p>
        <Button asChild variant="outline" className="mt-3">
          <Link to="/heures">Voir mes heures</Link>
        </Button>
      </Card>
    )
  }

  return (
    <Card className="mt-3">
      <p className="text-meta font-medium text-secondary">
        Prochaine facture : {draft.nextNumber}
      </p>

      <ul className="mt-3 flex flex-col gap-2">
        {draft.missions.map((mission) => (
          <li key={mission.timesheetId}>
            <label className="flex items-start gap-3">
              <input
                type="checkbox"
                checked={chosen.includes(mission.timesheetId)}
                onChange={() => toggle(mission.timesheetId)}
                className="mt-1 size-4 accent-accent"
              />
              <span className="flex-1">
                <span className="block text-base">
                  {mission.positionLabel} — {mission.eventTitle}
                </span>
                <span className="block text-meta text-secondary tabular-nums">
                  {formatDayOf(mission.startsAt)} · {mission.hours} h ×{' '}
                  {formatAmount(mission.unitRate)} = {formatAmount(mission.amount)}
                </span>
              </span>
            </label>
          </li>
        ))}
      </ul>

      <p className="mt-4 flex items-baseline justify-between gap-4">
        <span className="text-base text-secondary">Total</span>
        <span className="text-hero font-semibold tabular-nums">{formatAmount(total)}</span>
      </p>

      {draft.blockers.length > 0 && (
        <ul className="mt-3 flex flex-col gap-1">
          {draft.blockers.map((blocker) => (
            <li key={blocker} className="text-base text-warning">
              {blocker}
            </li>
          ))}
        </ul>
      )}

      {error && (
        <p role="alert" className="mt-3 text-base text-danger">
          {error}
        </p>
      )}

      <Button
        type="button"
        size="block"
        className="mt-4 xl:w-auto"
        onClick={() => issue.mutate()}
        disabled={issue.isPending || chosen.length === 0 || draft.blockers.length > 0}
      >
        {issue.isPending ? 'Génération…' : 'Générer la facture'}
      </Button>
    </Card>
  )
}

function InvoiceCard({ invoice }: { invoice: InvoiceView }) {
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)

  const open = useMutation({
    mutationFn: () => api.post<DocumentLink>(`/me/invoices/${invoice.id}/link`),
    onSuccess: (link) => window.open(link.url, '_blank', 'noopener'),
    onError: () => setError("Le PDF n'a pas pu être ouvert."),
  })

  const submit = useMutation({
    mutationFn: () => api.post<InvoiceView>(`/me/invoices/${invoice.id}/submit`),
    onSuccess: async () => {
      setError(null)
      await queryClient.invalidateQueries({ queryKey: ['invoices'] })
    },
    onError: (cause) => setError(cause instanceof ApiError ? cause.message : 'Le dépôt a échoué.'),
  })

  return (
    <Card>
      <div className="flex items-baseline justify-between gap-3">
        <p className="text-strong font-medium">{invoice.number}</p>
        <p
          className={cn(
            'text-meta font-medium',
            invoice.status === 'Paid'
              ? 'text-accent'
              : invoice.status === 'Cancelled'
                ? 'text-danger'
                : 'text-secondary',
          )}
        >
          {statusLabels[invoice.status] ?? invoice.status}
        </p>
      </div>

      <p className="mt-1 text-base text-secondary tabular-nums">
        période du {new Date(`${invoice.periodStart}T00:00:00`).toLocaleDateString('fr-FR')} au{' '}
        {new Date(`${invoice.periodEnd}T00:00:00`).toLocaleDateString('fr-FR')}
      </p>
      <p className="mt-2 text-hero font-semibold tabular-nums">
        {formatAmount(invoice.totalAmount)}
      </p>

      {error && (
        <p role="alert" className="mt-2 text-base text-danger">
          {error}
        </p>
      )}

      <div className="mt-3 flex gap-2">
        <Button type="button" variant="outline" onClick={() => open.mutate()}>
          Voir le PDF
        </Button>
        {invoice.status === 'Issued' && (
          <Button type="button" onClick={() => submit.mutate()} disabled={submit.isPending}>
            {submit.isPending ? 'Dépôt…' : 'Déposer'}
          </Button>
        )}
      </div>
    </Card>
  )
}
