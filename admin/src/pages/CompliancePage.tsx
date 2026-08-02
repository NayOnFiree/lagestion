import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { api, ApiError } from '@/lib/api'
import { documentTypeLabels, formatDate, formatFileSize } from '@/lib/labels'
import { cn } from '@/lib/utils'
import type { components } from '@/types/api'

type ComplianceDocument = components['schemas']['ComplianceDocument']
type ContractorCompliance = components['schemas']['ContractorCompliance']
type DocumentLink = components['schemas']['DocumentLink']

export function CompliancePage() {
  const { data: queue, isPending: queuePending } = useQuery({
    queryKey: ['compliance', 'documents'],
    queryFn: () => api.get<ComplianceDocument[]>('/compliance/documents'),
  })

  const { data: contractors, isPending: contractorsPending } = useQuery({
    queryKey: ['compliance', 'contractors'],
    queryFn: () => api.get<ContractorCompliance[]>('/compliance/contractors'),
  })

  return (
    <section>
      <h1 className="text-title font-semibold">Conformité documentaire</h1>
      <p className="mt-1 text-base text-secondary">
        Pièces à traiter, puis état des dossiers du réseau.
      </p>

      <h2 className="mt-6 text-strong font-medium">À traiter</h2>
      {queuePending ? <TableSkeleton /> : <QueueTable documents={queue ?? []} />}

      <h2 className="mt-8 text-strong font-medium">Dossiers</h2>
      {contractorsPending ? <TableSkeleton /> : <ContractorsTable contractors={contractors ?? []} />}
    </section>
  )
}

function QueueTable({ documents }: { documents: ComplianceDocument[] }) {
  if (documents.length === 0) {
    return (
      <div className="mt-3 rounded-card border border-border p-6">
        <p className="text-strong font-medium">Rien à traiter</p>
        <p className="mt-1 text-base text-secondary">
          Aucune pièce en attente, périmée ou proche de l'expiration.
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
            <th className="h-row px-3">Pièce</th>
            <th className="h-row px-3">Déposée le</th>
            <th className="h-row px-3">Validité</th>
            <th className="h-row px-3">Statut</th>
            <th className="h-row px-3 text-right">Actions</th>
          </tr>
        </thead>
        <tbody>
          {documents.map((document) => (
            <QueueRow key={document.id} document={document} />
          ))}
        </tbody>
      </table>
    </div>
  )
}

function QueueRow({ document }: { document: ComplianceDocument }) {
  const queryClient = useQueryClient()
  const [rejecting, setRejecting] = useState(false)
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['compliance'] })
  }

  const review = useMutation({
    mutationFn: (body: { approved: boolean; note?: string }) =>
      api.post<ComplianceDocument>(`/compliance/documents/${document.id}/review`, body),
    onSuccess: async () => {
      setRejecting(false)
      setNote('')
      setError(null)
      await refresh()
    },
    onError: (cause) =>
      setError(cause instanceof ApiError ? cause.message : 'La décision n’a pas pu être enregistrée.'),
  })

  const open = useMutation({
    mutationFn: () => api.post<DocumentLink>(`/compliance/documents/${document.id}/link`),
    onSuccess: (link) => window.open(link.url, '_blank', 'noopener'),
    onError: () => setError("Le document n'a pas pu être ouvert."),
  })

  return (
    <>
      <tr className="border-t border-border">
        <td className="h-row px-3">{document.contractorName}</td>
        <td className="h-row px-3">
          {documentTypeLabels[document.type] ?? document.type}
          <span className="ml-2 text-meta text-secondary">
            {formatFileSize(document.sizeBytes)}
          </span>
        </td>
        <td className="h-row px-3 tabular-nums text-secondary">
          {new Date(document.createdAt).toLocaleDateString('fr-FR')}
        </td>
        <td className="h-row px-3 tabular-nums">
          <ExpiryCell document={document} />
        </td>
        <td className="h-row px-3">
          <StatusBadge document={document} />
        </td>
        <td className="h-row px-3">
          <div className="flex justify-end gap-1">
            <Button type="button" variant="ghost" size="dense" onClick={() => open.mutate()}>
              Ouvrir
            </Button>
            {document.status !== 'Approved' && (
              <Button
                type="button"
                size="dense"
                onClick={() => review.mutate({ approved: true })}
                disabled={review.isPending}
              >
                Valider
              </Button>
            )}
            {document.status !== 'Rejected' && (
              <Button
                type="button"
                variant="outline"
                size="dense"
                onClick={() => setRejecting((value) => !value)}
              >
                Refuser
              </Button>
            )}
          </div>
        </td>
      </tr>

      {(rejecting || error) && (
        <tr className="border-t border-border bg-surface">
          <td colSpan={6} className="px-3 py-3">
            {error && (
              <p role="alert" className="mb-2 text-base text-danger">
                {error}
              </p>
            )}
            {rejecting && (
              <div className="flex items-center gap-2">
                <Input
                  value={note}
                  onChange={(event) => setNote(event.target.value)}
                  placeholder="Motif du refus, communiqué au prestataire"
                  className="h-8 flex-1"
                  aria-label="Motif du refus"
                />
                <Button
                  type="button"
                  size="dense"
                  onClick={() => review.mutate({ approved: false, note })}
                  disabled={review.isPending || note.trim().length === 0}
                >
                  Confirmer le refus
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  size="dense"
                  onClick={() => setRejecting(false)}
                >
                  Annuler
                </Button>
              </div>
            )}
          </td>
        </tr>
      )}
    </>
  )
}

function ExpiryCell({ document }: { document: ComplianceDocument }) {
  const expiry = formatDate(document.expiresAt)

  if (!expiry) {
    return <span className="text-secondary">—</span>
  }

  if (document.isExpired) {
    return <span className="text-danger">périmée le {expiry}</span>
  }

  const days = document.daysUntilExpiry ?? 0

  return (
    <span className={cn(days <= 30 && 'text-warning')}>
      {expiry}
      {days <= 30 && <span className="ml-1">({days} j)</span>}
    </span>
  )
}

function StatusBadge({ document }: { document: ComplianceDocument }) {
  const [label, tone] = document.isExpired
    ? ['périmée', 'text-danger']
    : document.status === 'Approved'
      ? ['validée', 'text-success']
      : document.status === 'Rejected'
        ? ['refusée', 'text-danger']
        : ['en attente', 'text-secondary']

  return <span className={tone}>{label}</span>
}

function ContractorsTable({ contractors }: { contractors: ContractorCompliance[] }) {
  if (contractors.length === 0) {
    return (
      <div className="mt-3 rounded-card border border-border p-6">
        <p className="text-strong font-medium">Aucun prestataire</p>
        <p className="mt-1 text-base text-secondary">Le réseau est vide.</p>
      </div>
    )
  }

  return (
    <div className="mt-3 overflow-x-auto rounded-card border border-border">
      <table className="w-full text-left text-dense">
        <thead className="sticky top-0 bg-surface text-meta font-medium text-secondary">
          <tr>
            <th className="h-row px-3">Prestataire</th>
            <th className="h-row px-3">Adresse</th>
            <th className="h-row px-3 text-right">Dossier</th>
            <th className="h-row px-3 text-right">En attente</th>
            <th className="h-row px-3 text-right">Périmées</th>
            <th className="h-row px-3 text-right">Bientôt</th>
            <th className="h-row px-3">Manquant</th>
          </tr>
        </thead>
        <tbody>
          {contractors.map((contractor) => (
            <tr key={contractor.contractorId} className="border-t border-border">
              <td className="h-row px-3">{contractor.contractorName}</td>
              <td className="h-row px-3 text-secondary">{contractor.email}</td>
              <td className="h-row px-3 text-right tabular-nums">
                <span
                  className={cn(contractor.completeness.isComplete ? 'text-success' : 'text-warning')}
                >
                  {contractor.completeness.satisfiedCount}/{contractor.completeness.totalCount}
                </span>
              </td>
              <td className="h-row px-3 text-right tabular-nums">{contractor.pendingCount}</td>
              <td
                className={cn(
                  'h-row px-3 text-right tabular-nums',
                  contractor.expiredCount > 0 && 'text-danger',
                )}
              >
                {contractor.expiredCount}
              </td>
              <td
                className={cn(
                  'h-row px-3 text-right tabular-nums',
                  contractor.expiringSoonCount > 0 && 'text-warning',
                )}
              >
                {contractor.expiringSoonCount}
              </td>
              <td className="h-row px-3 text-secondary">
                {contractor.completeness.missingDocumentTypes
                  .map((type) => documentTypeLabels[type] ?? type)
                  .join(', ') || '—'}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function TableSkeleton() {
  return <div className="mt-3 h-40 w-full rounded-card bg-surface" aria-busy="true" />
}
