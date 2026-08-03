import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { api, ApiError } from '@/lib/api'
import { formatMoment } from '@/lib/events'
import type { components } from '@/types/api'

type NotificationEntry = components['schemas']['NotificationEntry']

const statusLabels: Record<string, string> = {
  Pending: 'en attente',
  Sent: 'envoyé',
  Failed: 'en échec',
}

const templateLabels: Record<string, string> = {
  'mission-proposee': 'proposition de mission',
  'mission-confirmee': 'mission confirmée',
  'mission-annulee': 'mission annulée',
  'mission-rappel': 'rappel de mission',
  'document-refuse': 'document refusé',
  'document-expirant': 'document expirant',
  'heures-contestees': 'heures contestées',
  'facture-a-deposer': 'facture à déposer',
  'facture-payee': 'facture payée',
}

export function NotificationsPage() {
  const [status, setStatus] = useState('all')

  const { data, isPending } = useQuery({
    queryKey: ['notifications', status],
    queryFn: () =>
      api.get<NotificationEntry[]>(
        `/notifications${status === 'all' ? '' : `?status=${status}`}`,
      ),
    refetchInterval: 30_000,
  })

  return (
    <section>
      <h1 className="text-title font-semibold">Envois</h1>
      <p className="mt-1 text-base text-secondary">
        Le mail est le canal fiable et obligatoire : un message qui ne part pas doit se voir.
      </p>

      <div className="mt-4 flex items-center gap-3">
        <Select value={status} onValueChange={setStatus}>
          <SelectTrigger className="h-8 w-48" aria-label="Filtrer par statut">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">tous les statuts</SelectItem>
            <SelectItem value="Pending">en attente</SelectItem>
            <SelectItem value="Sent">envoyé</SelectItem>
            <SelectItem value="Failed">en échec</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {isPending ? (
        <div className="mt-4 h-64 w-full rounded-card bg-surface" aria-busy="true" />
      ) : (
        <NotificationsTable entries={data ?? []} />
      )}
    </section>
  )
}

function NotificationsTable({ entries }: { entries: NotificationEntry[] }) {
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)

  const retry = useMutation({
    mutationFn: (id: string) => api.post<void>(`/notifications/${id}/retry`),
    onSuccess: async () => {
      setError(null)
      await queryClient.invalidateQueries({ queryKey: ['notifications'] })
    },
    onError: (cause) =>
      setError(cause instanceof ApiError ? cause.message : 'La relance a échoué.'),
  })

  if (entries.length === 0) {
    return (
      <div className="mt-4 rounded-card border border-border p-6">
        <p className="text-strong font-medium">Aucun envoi</p>
        <p className="mt-1 text-base text-secondary">
          Les messages apparaîtront ici dès qu'une action en déclenchera un.
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

      <div className="mt-4 overflow-x-auto rounded-card border border-border">
        <table className="w-full text-left text-dense">
          <thead className="sticky top-0 bg-surface text-meta font-medium text-secondary">
            <tr>
              <th className="h-row px-3">Message</th>
              <th className="h-row px-3">Destinataire</th>
              <th className="h-row px-3">Mis en file</th>
              <th className="h-row px-3">Envoyé</th>
              <th className="h-row px-3 text-right">Tentatives</th>
              <th className="h-row px-3">Statut</th>
              <th className="h-row px-3" />
            </tr>
          </thead>
          <tbody>
            {entries.map((entry) => (
              <tr key={entry.id} className="group border-t border-border hover:bg-surface">
                <td className="h-row px-3">
                  {templateLabels[entry.template] ?? entry.template}
                  {entry.lastError && (
                    <span className="ml-2 text-danger">{entry.lastError}</span>
                  )}
                </td>
                <td className="h-row px-3">
                  {entry.recipientName}
                  <span className="ml-2 text-secondary">{entry.recipient}</span>
                </td>
                <td className="h-row px-3 tabular-nums text-secondary">
                  {formatMoment(entry.createdAt)}
                </td>
                <td className="h-row px-3 tabular-nums text-secondary">
                  {entry.sentAt ? formatMoment(entry.sentAt) : '—'}
                </td>
                <td className="h-row px-3 text-right tabular-nums">{entry.attempts}</td>
                <td className="h-row px-3">
                  <Badge
                    tone={
                      entry.status === 'Sent'
                        ? 'accent'
                        : entry.status === 'Failed'
                          ? 'danger'
                          : 'neutral'
                    }
                  >
                    {statusLabels[entry.status] ?? entry.status}
                  </Badge>
                </td>
                <td className="h-row px-3">
                  {entry.status === 'Failed' && (
                    <div className="flex justify-end opacity-0 group-hover:opacity-100 group-focus-within:opacity-100">
                      <Button
                        type="button"
                        variant="ghost"
                        size="dense"
                        onClick={() => retry.mutate(entry.id)}
                      >
                        Relancer
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
