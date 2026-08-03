import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Field } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { api, ApiError } from '@/lib/api'
import { formatRange, formatRate, fromLocalInput, toLocalInput } from '@/lib/events'
import { documentTypeLabels } from '@/lib/labels'
import { cn } from '@/lib/utils'
import type { components } from '@/types/api'

type PositionStaffing = components['schemas']['PositionStaffing']
type Candidate = components['schemas']['Candidate']
type AssignmentRow = components['schemas']['AssignmentRow']

const assignmentLabels: Record<string, string> = {
  Proposed: 'en attente de réponse',
  Accepted: 'a accepté',
  Declined: 'a refusé',
  Confirmed: 'confirmé',
  Cancelled: 'annulé',
}

export function StaffingPage() {
  const { id = '' } = useParams()
  const [skill, setSkill] = useState('')

  const { data: staffing, isPending } = useQuery({
    queryKey: ['staffing', id],
    queryFn: () => api.get<PositionStaffing>(`/positions/${id}/staffing`),
  })

  const { data: candidates } = useQuery({
    queryKey: ['candidates', id, skill],
    queryFn: () =>
      api.get<Candidate[]>(
        `/positions/${id}/candidates${skill ? `?skill=${encodeURIComponent(skill)}` : ''}`,
      ),
  })

  if (isPending) {
    return <div className="h-64 w-full rounded-card bg-surface" aria-busy="true" />
  }

  if (!staffing) {
    return (
      <section>
        <h1 className="text-title font-semibold">Poste introuvable</h1>
      </section>
    )
  }

  const remaining = staffing.headcount - staffing.confirmedCount

  return (
    <section>
      <Link to="/evenements" className="text-meta text-secondary hover:text-primary">
        ← Événements
      </Link>

      <div className="mt-2">
        <h1 className="text-title font-semibold">{staffing.label}</h1>
        <p className="mt-1 text-base text-secondary tabular-nums">
          {staffing.eventTitle} — {formatRange(staffing.startsAt, staffing.endsAt)} —{' '}
          {formatRate(staffing.hourlyRate)}/h
        </p>
        <p className="mt-2 text-base">
          <span className={cn('font-medium tabular-nums', remaining > 0 && 'text-warning')}>
            {staffing.confirmedCount}/{staffing.headcount}
          </span>{' '}
          {staffing.confirmedCount > 1 ? 'confirmés' : 'confirmé'}
          {remaining > 0 && ` — ${remaining} place${remaining > 1 ? 's' : ''} à pourvoir`}
        </p>
      </div>

      <h2 className="mt-6 text-strong font-medium">Propositions envoyées</h2>
      <AssignmentsTable positionId={id} assignments={staffing.assignments} />

      <div className="mt-8 flex items-center justify-between gap-4">
        <h2 className="text-strong font-medium">Prestataires disponibles</h2>
      </div>
      <div className="mt-3 flex items-center gap-3">
        <Input
          value={skill}
          onChange={(event) => setSkill(event.target.value)}
          placeholder="Filtrer par compétence"
          aria-label="Filtrer par compétence"
          className="h-8 w-64"
        />
      </div>

      <CandidatesTable positionId={id} candidates={candidates ?? []} />
    </section>
  )
}

function AssignmentsTable({
  positionId,
  assignments,
}: {
  positionId: string
  assignments: AssignmentRow[]
}) {
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)

  const act = useMutation({
    mutationFn: ({ assignmentId, action }: { assignmentId: string; action: string }) =>
      api.post<PositionStaffing>(`/assignments/${assignmentId}/${action}`),
    onSuccess: async () => {
      setError(null)
      await queryClient.invalidateQueries({ queryKey: ['staffing', positionId] })
      await queryClient.invalidateQueries({ queryKey: ['candidates', positionId] })
    },
    onError: (cause) =>
      setError(cause instanceof ApiError ? cause.message : "L'opération a échoué."),
  })

  if (assignments.length === 0) {
    return (
      <div className="mt-3 rounded-card border border-border p-6">
        <p className="text-strong font-medium">Aucune proposition</p>
        <p className="mt-1 text-base text-secondary">
          Sélectionnez des prestataires disponibles ci-dessous pour leur proposer la mission.
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
              <th className="h-row px-3">État</th>
              <th className="h-row px-3">Proposée le</th>
              <th className="h-row px-3">Réponse attendue</th>
              <th className="h-row px-3" />
            </tr>
          </thead>
          <tbody>
            {assignments.map((assignment) => (
              <tr key={assignment.id} className="group border-t border-border hover:bg-surface">
                <td className="h-row px-3 font-medium">{assignment.contractorName}</td>
                <td className="h-row px-3">
                  <Badge
                    tone={
                      assignment.status === 'Confirmed'
                        ? 'accent'
                        : assignment.status === 'Declined' || assignment.status === 'Cancelled'
                          ? 'danger'
                          : assignment.isExpired
                            ? 'warning'
                            : 'neutral'
                    }
                  >
                    {assignment.isExpired && assignment.status === 'Proposed'
                      ? 'sans réponse, délai dépassé'
                      : (assignmentLabels[assignment.status] ?? assignment.status)}
                  </Badge>
                </td>
                <td className="h-row px-3 tabular-nums text-secondary">
                  {new Date(assignment.proposedAt).toLocaleDateString('fr-FR')}
                </td>
                <td className="h-row px-3 tabular-nums text-secondary">
                  {assignment.responseDeadline
                    ? new Date(assignment.responseDeadline).toLocaleDateString('fr-FR')
                    : 'sans délai'}
                </td>
                <td className="h-row px-3">
                  <div className="flex justify-end gap-1 opacity-0 group-hover:opacity-100 group-focus-within:opacity-100">
                    {assignment.status === 'Accepted' && (
                      <Button
                        type="button"
                        size="dense"
                        onClick={() => act.mutate({ assignmentId: assignment.id, action: 'confirm' })}
                      >
                        Confirmer
                      </Button>
                    )}
                    {(assignment.status === 'Proposed' ||
                      assignment.status === 'Accepted' ||
                      assignment.status === 'Confirmed') && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="dense"
                        onClick={() => act.mutate({ assignmentId: assignment.id, action: 'cancel' })}
                      >
                        {assignment.status === 'Confirmed' ? 'Désister' : 'Retirer'}
                      </Button>
                    )}
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

function CandidatesTable({
  positionId,
  candidates,
}: {
  positionId: string
  candidates: Candidate[]
}) {
  const queryClient = useQueryClient()
  const [selected, setSelected] = useState<string[]>([])
  const [deadline, setDeadline] = useState(() => {
    const date = new Date()
    date.setDate(date.getDate() + 3)
    date.setHours(18, 0, 0, 0)
    return toLocalInput(date.toISOString())
  })
  const [error, setError] = useState<string | null>(null)

  const propose = useMutation({
    mutationFn: () =>
      api.post<PositionStaffing>(`/positions/${positionId}/assignments`, {
        contractorIds: selected,
        responseDeadline: deadline ? fromLocalInput(deadline) : null,
      }),
    onSuccess: async () => {
      setError(null)
      setSelected([])
      await queryClient.invalidateQueries({ queryKey: ['staffing', positionId] })
      await queryClient.invalidateQueries({ queryKey: ['candidates', positionId] })
    },
    onError: (cause) => setError(cause instanceof ApiError ? cause.message : "L'envoi a échoué."),
  })

  const toggle = (contractorId: string) =>
    setSelected((current) =>
      current.includes(contractorId)
        ? current.filter((item) => item !== contractorId)
        : [...current, contractorId],
    )

  if (candidates.length === 0) {
    return (
      <div className="mt-3 rounded-card border border-border p-6">
        <p className="text-strong font-medium">Aucun prestataire disponible</p>
        <p className="mt-1 text-base text-secondary">
          Personne n'est déclaré disponible sur ce créneau, ou tous ont déjà été sollicités.
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
              <th className="h-row w-10 px-3" />
              <th className="h-row px-3">Prestataire</th>
              <th className="h-row px-3">Ville</th>
              <th className="h-row px-3 text-right">Rayon</th>
              <th className="h-row px-3">Compétences</th>
              <th className="h-row px-3 text-right">Tarif habituel</th>
              <th className="h-row px-3">Dossier</th>
            </tr>
          </thead>
          <tbody>
            {candidates.map((candidate) => (
              <tr key={candidate.contractorId} className="border-t border-border hover:bg-surface">
                <td className="h-row px-3">
                  <input
                    type="checkbox"
                    checked={selected.includes(candidate.contractorId)}
                    onChange={() => toggle(candidate.contractorId)}
                    aria-label={`Sélectionner ${candidate.name}`}
                    className="size-4 accent-accent"
                  />
                </td>
                <td className="h-row px-3 font-medium">{candidate.name}</td>
                <td className="h-row px-3 text-secondary">{candidate.baseCity ?? '—'}</td>
                <td className="h-row px-3 text-right tabular-nums text-secondary">
                  {candidate.travelRadiusKm ? `${candidate.travelRadiusKm} km` : '—'}
                </td>
                <td className="h-row px-3 text-secondary">
                  {candidate.skills.join(', ') || '—'}
                </td>
                <td className="h-row px-3 text-right tabular-nums text-secondary">
                  {candidate.defaultHourlyRate ? formatRate(candidate.defaultHourlyRate) : '—'}
                </td>
                <td className="h-row px-3">
                  {candidate.dossierComplete ? (
                    <span className="text-secondary">complet</span>
                  ) : (
                    <span className="text-warning">
                      manque{' '}
                      {candidate.missingDocumentTypes
                        .map((type) => documentTypeLabels[type] ?? type)
                        .join(', ')}
                    </span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="mt-4 flex items-end justify-between gap-4">
        <Field
          id="deadline"
          label="Réponse attendue avant"
          hint="Laissez vide pour ne pas fixer de délai"
          className="w-64"
        >
          <Input
            id="deadline"
            type="datetime-local"
            value={deadline}
            onChange={(event) => setDeadline(event.target.value)}
            className="h-8"
          />
        </Field>

        <Button
          type="button"
          onClick={() => propose.mutate()}
          disabled={selected.length === 0 || propose.isPending}
        >
          {propose.isPending
            ? 'Envoi…'
            : `Proposer à ${selected.length || ''} prestataire${selected.length > 1 ? 's' : ''}`}
        </Button>
      </div>
    </>
  )
}
