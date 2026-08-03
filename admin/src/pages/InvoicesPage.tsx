import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { api, ApiError } from '@/lib/api'
import { formatRate } from '@/lib/events'
import type { components } from '@/types/api'

type InvoiceView = components['schemas']['InvoiceView']
type DocumentLink = components['schemas']['DocumentLink']

const statusLabels: Record<string, string> = {
  Submitted: 'déposée',
  Validated: 'validée',
  Paid: 'payée',
  Cancelled: 'annulée',
}

export function InvoicesPage() {
  const [status, setStatus] = useState('all')

  const { data, isPending } = useQuery({
    queryKey: ['invoices', status],
    queryFn: () => api.get<InvoiceView[]>(`/invoices${status === 'all' ? '' : `?status=${status}`}`),
  })

  return (
    <section>
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-title font-semibold">Factures</h1>
          <p className="mt-1 text-base text-secondary">
            Déposées par les prestataires. Elles restent leurs factures : l'agence contrôle et
            paie, elle ne renumérote pas.
          </p>
        </div>
        <Button
          type="button"
          variant="outline"
          onClick={() => void api.download('/invoices/export', 'factures.csv')}
        >
          Export comptable
        </Button>
      </div>

      <div className="mt-4 flex items-center gap-3">
        <Select value={status} onValueChange={setStatus}>
          <SelectTrigger className="h-8 w-48" aria-label="Filtrer par statut">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">tous les statuts</SelectItem>
            <SelectItem value="Submitted">déposée</SelectItem>
            <SelectItem value="Validated">validée</SelectItem>
            <SelectItem value="Paid">payée</SelectItem>
            <SelectItem value="Cancelled">annulée</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {isPending ? (
        <div className="mt-4 h-64 w-full rounded-card bg-surface" aria-busy="true" />
      ) : (
        <InvoicesTable invoices={data ?? []} />
      )}
    </section>
  )
}

function InvoicesTable({ invoices }: { invoices: InvoiceView[] }) {
  if (invoices.length === 0) {
    return (
      <div className="mt-4 rounded-card border border-border p-6">
        <p className="text-strong font-medium">Aucune facture</p>
        <p className="mt-1 text-base text-secondary">
          Les factures apparaissent ici une fois déposées par les prestataires.
        </p>
      </div>
    )
  }

  return (
    <div className="mt-4 overflow-x-auto rounded-card border border-border">
      <table className="w-full text-left text-dense">
        <thead className="sticky top-0 bg-surface text-meta font-medium text-secondary">
          <tr>
            <th className="h-row px-3">Numéro</th>
            <th className="h-row px-3">Prestataire</th>
            <th className="h-row px-3">Période</th>
            <th className="h-row px-3">Déposée le</th>
            <th className="h-row px-3 text-right">Montant</th>
            <th className="h-row px-3">Statut</th>
            <th className="h-row px-3" />
          </tr>
        </thead>
        <tbody>
          {invoices.map((invoice) => (
            <InvoiceRow key={invoice.id} invoice={invoice} />
          ))}
        </tbody>
      </table>
    </div>
  )
}

function InvoiceRow({ invoice }: { invoice: InvoiceView }) {
  const queryClient = useQueryClient()
  const [cancelling, setCancelling] = useState(false)
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string | null>(null)

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['invoices'] })
  }

  const act = useMutation({
    mutationFn: ({ action, body }: { action: string; body?: unknown }) =>
      api.post<InvoiceView>(`/invoices/${invoice.id}/${action}`, body),
    onSuccess: async () => {
      setError(null)
      setCancelling(false)
      setReason('')
      await refresh()
    },
    onError: (cause) =>
      setError(cause instanceof ApiError ? cause.message : "L'opération a échoué."),
  })

  const open = useMutation({
    mutationFn: () => api.post<DocumentLink>(`/invoices/${invoice.id}/link`),
    onSuccess: (link) => window.open(link.url, '_blank', 'noopener'),
    onError: () => setError("Le PDF n'a pas pu être ouvert."),
  })

  const day = (iso: string | null | undefined) =>
    iso ? new Date(iso).toLocaleDateString('fr-FR') : '—'

  return (
    <>
      <tr className="group border-t border-border hover:bg-surface">
        <td className="h-row px-3 font-medium tabular-nums">{invoice.number}</td>
        <td className="h-row px-3">{invoice.contractorName}</td>
        <td className="h-row px-3 tabular-nums text-secondary">
          {new Date(`${invoice.periodStart}T00:00:00`).toLocaleDateString('fr-FR')} –{' '}
          {new Date(`${invoice.periodEnd}T00:00:00`).toLocaleDateString('fr-FR')}
        </td>
        <td className="h-row px-3 tabular-nums text-secondary">{day(invoice.submittedAt)}</td>
        <td className="h-row px-3 text-right tabular-nums">{formatRate(invoice.totalAmount)}</td>
        <td className="h-row px-3">
          <Badge
            tone={
              invoice.status === 'Paid'
                ? 'accent'
                : invoice.status === 'Cancelled'
                  ? 'danger'
                  : 'neutral'
            }
          >
            {statusLabels[invoice.status] ?? invoice.status}
          </Badge>
        </td>
        <td className="h-row px-3">
          <div className="flex justify-end gap-1 opacity-0 group-hover:opacity-100 group-focus-within:opacity-100">
            <Button type="button" variant="ghost" size="dense" onClick={() => open.mutate()}>
              PDF
            </Button>
            {invoice.status === 'Submitted' && (
              <Button
                type="button"
                size="dense"
                onClick={() => act.mutate({ action: 'validate' })}
              >
                Valider
              </Button>
            )}
            {invoice.status === 'Validated' && (
              <Button type="button" size="dense" onClick={() => act.mutate({ action: 'pay' })}>
                Marquer payée
              </Button>
            )}
            {invoice.status !== 'Paid' && invoice.status !== 'Cancelled' && (
              <Button
                type="button"
                variant="outline"
                size="dense"
                onClick={() => setCancelling((value) => !value)}
              >
                Annuler
              </Button>
            )}
          </div>
        </td>
      </tr>

      {(cancelling || error) && (
        <tr className="border-t border-border bg-surface">
          <td colSpan={7} className="px-3 py-3">
            {error && (
              <p role="alert" className="mb-2 text-base text-danger">
                {error}
              </p>
            )}
            {cancelling && (
              <div className="flex items-center gap-2">
                <Input
                  value={reason}
                  onChange={(event) => setReason(event.target.value)}
                  placeholder="Motif de l'annulation"
                  aria-label="Motif de l'annulation"
                  className="h-8 flex-1"
                />
                <Button
                  type="button"
                  size="dense"
                  disabled={reason.trim().length === 0}
                  onClick={() => act.mutate({ action: 'cancel', body: { reason } })}
                >
                  Confirmer l'annulation
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  size="dense"
                  onClick={() => setCancelling(false)}
                >
                  Fermer
                </Button>
              </div>
            )}
            <p className="mt-2 text-meta text-secondary">
              Une facture annulée garde son numéro : la séquence du prestataire doit rester sans
              trou.
            </p>
          </td>
        </tr>
      )}
    </>
  )
}
