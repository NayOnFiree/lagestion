import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useRef, useState } from 'react'
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
import {
  documentStatusLabels,
  documentTypeLabels,
  formatDate,
  formatFileSize,
  profileFieldLabels,
} from '@/lib/labels'
import { cn } from '@/lib/utils'
import type { components } from '@/types/api'

type DocumentVault = components['schemas']['DocumentVault']
type DocumentSummary = components['schemas']['DocumentSummary']
type DocumentLink = components['schemas']['DocumentLink']

export function DocumentsPage() {
  const queryClient = useQueryClient()

  const { data, isPending } = useQuery({
    queryKey: ['documents'],
    queryFn: () => api.get<DocumentVault>('/me/documents'),
  })

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['documents'] })

  if (isPending) {
    return <VaultSkeleton />
  }

  return (
    <section>
      <h2 className="text-title font-semibold">Mes documents</h2>
      <p className="mt-1 text-base text-secondary">
        L'agence a besoin de ces pièces pour vous proposer des missions.
      </p>

      {data && <CompletenessCard completeness={data.completeness} />}

      <UploadForm onUploaded={refresh} />

      <h3 className="mt-8 text-strong font-medium">Pièces déposées</h3>

      {data?.documents.length === 0 ? (
        <p className="mt-1 text-base text-secondary">
          Aucune pièce pour le moment. Déposez-en une ci-dessus.
        </p>
      ) : (
        <ul className="mt-3 flex flex-col gap-3">
          {data?.documents.map((document) => (
            <li key={document.id}>
              <DocumentCard document={document} onChanged={refresh} />
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

function CompletenessCard({
  completeness,
}: {
  completeness: components['schemas']['DossierCompleteness']
}) {
  const missing = [
    ...completeness.missingDocumentTypes.map((type) => documentTypeLabels[type] ?? type),
    ...completeness.missingProfileFields.map((field) => profileFieldLabels[field] ?? field),
  ]

  return (
    <Card className={cn('mt-6', completeness.isComplete && 'border-accent bg-accent-weak')}>
      <p className="text-strong font-medium">
        {completeness.isComplete ? 'Dossier complet' : 'Dossier incomplet'}
      </p>
      <p className="mt-1 text-base text-secondary">
        {completeness.satisfiedCount} sur {completeness.totalCount} éléments fournis.
      </p>

      {missing.length > 0 && (
        <ul className="mt-3 flex flex-col gap-1">
          {missing.map((item) => (
            <li key={item} className="text-base text-secondary">
              — {item}
            </li>
          ))}
        </ul>
      )}
    </Card>
  )
}

function UploadForm({ onUploaded }: { onUploaded: () => Promise<void> }) {
  const fileInput = useRef<HTMLInputElement>(null)
  const [type, setType] = useState('IdentityCard')
  const [expiresAt, setExpiresAt] = useState('')
  const [issuedAt, setIssuedAt] = useState('')
  const [error, setError] = useState<string | null>(null)

  const upload = useMutation({
    mutationFn: (form: FormData) => api.postForm<DocumentSummary>('/me/documents', form),
    onSuccess: async () => {
      setError(null)
      setIssuedAt('')
      setExpiresAt('')
      if (fileInput.current) {
        fileInput.current.value = ''
      }
      await onUploaded()
    },
    onError: (cause) => {
      setError(
        cause instanceof ApiError && cause.status === 400
          ? cause.message
          : "Le dépôt a échoué. Réessayez dans un instant.",
      )
    },
  })

  const submit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()

    const file = fileInput.current?.files?.[0]

    if (!file) {
      setError('Choisissez un fichier.')
      return
    }

    const form = new FormData()
    form.append('Type', type)
    form.append('File', file)
    if (issuedAt) form.append('IssuedAt', issuedAt)
    if (expiresAt) form.append('ExpiresAt', expiresAt)

    upload.mutate(form)
  }

  return (
    <form onSubmit={submit} className="mt-6 flex flex-col gap-4">
      <h3 className="text-strong font-medium">Déposer une pièce</h3>

      <Field id="documentType" label="Nature du document">
        <Select value={type} onValueChange={setType}>
          <SelectTrigger id="documentType">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {Object.entries(documentTypeLabels).map(([value, label]) => (
              <SelectItem key={value} value={value}>
                {label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </Field>

      <Field id="issuedAt" label="Date de délivrance">
        <Input
          id="issuedAt"
          type="date"
          value={issuedAt}
          onChange={(event) => setIssuedAt(event.target.value)}
        />
      </Field>

      <Field id="expiresAt" label="Valable jusqu'au">
        <Input
          id="expiresAt"
          type="date"
          value={expiresAt}
          onChange={(event) => setExpiresAt(event.target.value)}
        />
      </Field>

      <Field id="file" label="Fichier" hint="PDF, JPEG ou PNG, 10 Mo au maximum">
        <input
          id="file"
          ref={fileInput}
          type="file"
          accept="application/pdf,image/jpeg,image/png"
          className={cn(
            'w-full rounded-control border border-border bg-bg p-2 text-base',
            'file:mr-3 file:rounded-control file:border-0 file:bg-surface file:px-3 file:py-2',
            'file:text-base file:font-medium file:text-primary',
          )}
        />
      </Field>

      {error && (
        <p role="alert" className="text-base text-danger">
          {error}
        </p>
      )}

      <Button type="submit" size="block" variant="outline" disabled={upload.isPending}>
        {upload.isPending ? 'Dépôt en cours…' : 'Déposer'}
      </Button>
    </form>
  )
}

function DocumentCard({
  document: item,
  onChanged,
}: {
  document: DocumentSummary
  onChanged: () => Promise<void>
}) {
  const [error, setError] = useState<string | null>(null)

  const open = useMutation({
    mutationFn: () => api.post<DocumentLink>(`/me/documents/${item.id}/link`),
    onSuccess: (link) => {
      window.open(link.url, '_blank', 'noopener')
    },
    onError: () => setError("Le document n'a pas pu être ouvert."),
  })

  const remove = useMutation({
    mutationFn: () => api.delete<void>(`/me/documents/${item.id}`),
    onSuccess: onChanged,
    onError: (cause) =>
      setError(
        cause instanceof ApiError && cause.status === 409
          ? cause.message
          : 'La suppression a échoué.',
      ),
  })

  const expiry = formatDate(item.expiresAt)

  return (
    <Card>
      <p className="text-strong font-medium">{documentTypeLabels[item.type] ?? item.type}</p>
      <p className="mt-0.5 text-meta text-secondary">
        {item.originalFileName} — {formatFileSize(item.sizeBytes)}
      </p>

      <p
        className={cn(
          'mt-2 text-base',
          item.isExpired || item.status === 'Rejected'
            ? 'text-danger'
            : item.status === 'Approved'
              ? 'text-success'
              : 'text-secondary',
        )}
      >
        {item.isExpired ? 'périmée' : documentStatusLabels[item.status] ?? item.status}
      </p>

      {expiry && (
        <p className="mt-0.5 text-meta text-secondary">
          {item.isExpired ? 'A expiré le' : 'Valable jusqu’au'} {expiry}
        </p>
      )}

      {item.status === 'Rejected' && item.reviewNote && (
        <p className="mt-2 text-base text-danger">Motif : {item.reviewNote}</p>
      )}

      {error && (
        <p role="alert" className="mt-2 text-base text-danger">
          {error}
        </p>
      )}

      <div className="mt-3 flex gap-2">
        <Button type="button" variant="outline" onClick={() => open.mutate()} disabled={open.isPending}>
          Consulter
        </Button>
        {item.status !== 'Approved' && (
          <Button
            type="button"
            variant="ghost"
            onClick={() => remove.mutate()}
            disabled={remove.isPending}
          >
            Supprimer
          </Button>
        )}
      </div>
    </Card>
  )
}

function VaultSkeleton() {
  return (
    <section aria-busy="true">
      <div className="h-7 w-44 rounded-control bg-surface" />
      <div className="mt-6 h-28 w-full rounded-card bg-surface" />
      <div className="mt-6 h-64 w-full rounded-card bg-surface" />
    </section>
  )
}
