import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent } from '@/components/ui/dialog'
import { Field } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { api, ApiError } from '@/lib/api'
import { formatMoment, formatRate } from '@/lib/events'
import { cn } from '@/lib/utils'
import type { components } from '@/types/api'

type NetworkContractor = components['schemas']['NetworkContractor']
type ContractorProfileDetail = components['schemas']['ContractorProfileDetail']
type Indicator = components['schemas']['Indicator']

/** Pourcentage lisible, ou tiret si l'indicateur n'a pas de données. */
function percent(indicator: Indicator) {
  return indicator.value === null ? '—' : `${Math.round(indicator.value * 100)} %`
}

export function NetworkPage() {
  const [selected, setSelected] = useState<string | null>(null)

  const { data, isPending } = useQuery({
    queryKey: ['network'],
    queryFn: () => api.get<NetworkContractor[]>('/network'),
  })

  return (
    <section>
      <h1 className="text-title font-semibold">Réseau</h1>
      <p className="mt-1 text-base text-secondary">
        Classés par score. Un prestataire sans historique n'a pas de score — ce n'est pas un
        mauvais score.
      </p>

      {isPending ? (
        <div className="mt-4 h-64 w-full rounded-card bg-surface" aria-busy="true" />
      ) : (
        <NetworkTable contractors={data ?? []} onOpen={setSelected} />
      )}

      {selected && <ContractorDialog contractorId={selected} onClose={() => setSelected(null)} />}
    </section>
  )
}

function NetworkTable({
  contractors,
  onOpen,
}: {
  contractors: NetworkContractor[]
  onOpen: (id: string) => void
}) {
  if (contractors.length === 0) {
    return (
      <div className="mt-4 rounded-card border border-border p-6">
        <p className="text-strong font-medium">Réseau vide</p>
        <p className="mt-1 text-base text-secondary">Aucun prestataire rattaché à l'agence.</p>
      </div>
    )
  }

  return (
    <div className="mt-4 overflow-x-auto rounded-card border border-border">
      <table className="w-full text-left text-dense">
        <thead className="sticky top-0 bg-surface text-meta font-medium text-secondary">
          <tr>
            <th className="h-row px-3 text-right">Score</th>
            <th className="h-row px-3">Prestataire</th>
            <th className="h-row px-3">Ville</th>
            <th className="h-row px-3">Compétences</th>
            <th className="h-row px-3 text-right">Acceptation</th>
            <th className="h-row px-3 text-right">Fiabilité</th>
            <th className="h-row px-3 text-right">Note</th>
            <th className="h-row px-3 text-right">Missions</th>
            <th className="h-row px-3">Dossier</th>
            <th className="h-row px-3" />
          </tr>
        </thead>
        <tbody>
          {contractors.map((contractor) => {
            const score = contractor.score

            return (
              <tr key={contractor.contractorId} className="group border-t border-border hover:bg-surface">
                <td className="h-row px-3 text-right">
                  <span
                    className={cn(
                      'font-medium tabular-nums',
                      score.score === null
                        ? 'text-muted'
                        : score.score >= 80
                          ? 'text-accent'
                          : score.score < 50
                            ? 'text-danger'
                            : 'text-warning',
                    )}
                  >
                    {score.score ?? '—'}
                  </span>
                </td>
                <td className="h-row px-3 font-medium">{contractor.name}</td>
                <td className="h-row px-3 text-secondary">{contractor.baseCity ?? '—'}</td>
                <td className="h-row px-3 text-secondary">{contractor.skills.join(', ') || '—'}</td>
                <td className="h-row px-3 text-right tabular-nums">
                  {percent(score.acceptance)}
                  <span className="ml-1 text-secondary">
                    ({score.acceptance.numerator}/{score.acceptance.denominator})
                  </span>
                </td>
                <td className="h-row px-3 text-right tabular-nums">
                  {percent(score.reliability)}
                  <span className="ml-1 text-secondary">
                    ({score.reliability.numerator}/{score.reliability.denominator})
                  </span>
                </td>
                <td className="h-row px-3 text-right tabular-nums">
                  {score.averageRating === null ? (
                    <span className="text-muted">—</span>
                  ) : (
                    <>
                      {score.averageRating}/5
                      <span className="ml-1 text-secondary">({score.ratingCount})</span>
                    </>
                  )}
                </td>
                <td className="h-row px-3 text-right tabular-nums">{score.completedMissions}</td>
                <td className="h-row px-3">
                  <Badge tone={contractor.dossierComplete ? 'neutral' : 'warning'}>
                    {contractor.dossierComplete ? 'complet' : 'incomplet'}
                  </Badge>
                </td>
                <td className="h-row px-3">
                  <div className="flex justify-end opacity-0 group-hover:opacity-100 group-focus-within:opacity-100">
                    <Button
                      type="button"
                      variant="ghost"
                      size="dense"
                      onClick={() => onOpen(contractor.contractorId)}
                    >
                      Ouvrir
                    </Button>
                  </div>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}

function ContractorDialog({
  contractorId,
  onClose,
}: {
  contractorId: string
  onClose: () => void
}) {
  const { data } = useQuery({
    queryKey: ['network', contractorId],
    queryFn: () => api.get<ContractorProfileDetail>(`/network/${contractorId}`),
  })

  return (
    <Dialog open onOpenChange={onClose}>
      <DialogContent
        title={data?.contractor.name ?? 'Prestataire'}
        description="Le score se recalcule à chaque lecture : il est toujours à jour."
      >
        {!data ? (
          <div className="h-40 rounded-card bg-surface" aria-busy="true" />
        ) : (
          <div className="flex flex-col gap-6">
            <dl className="grid grid-cols-2 gap-3">
              <Detail label="Score" value={data.contractor.score.score?.toString() ?? 'sans historique'} />
              <Detail label="Acceptation" value={percent(data.contractor.score.acceptance)} />
              <Detail label="Fiabilité" value={percent(data.contractor.score.reliability)} />
              <Detail
                label="Note moyenne"
                value={
                  data.contractor.score.averageRating === null
                    ? 'aucune appréciation'
                    : `${data.contractor.score.averageRating}/5`
                }
              />
              <Detail
                label="Tarif habituel"
                value={
                  data.contractor.defaultHourlyRate
                    ? formatRate(data.contractor.defaultHourlyRate)
                    : '—'
                }
              />
              <Detail
                label="Missions effectuées"
                value={data.contractor.score.completedMissions.toString()}
              />
            </dl>

            {data.unrated.length > 0 && (
              <div>
                <h3 className="text-strong font-medium">À apprécier</h3>
                <p className="mt-1 text-base text-secondary">
                  Facultatif : une prestation peut rester sans appréciation.
                </p>
                <ul className="mt-3 flex flex-col gap-3">
                  {data.unrated.map((mission) => (
                    <li key={mission.assignmentId}>
                      <RateForm mission={mission} contractorId={contractorId} />
                    </li>
                  ))}
                </ul>
              </div>
            )}

            <div>
              <h3 className="text-strong font-medium">Appréciations</h3>
              {data.ratings.length === 0 ? (
                <p className="mt-1 text-base text-secondary">Aucune appréciation pour le moment.</p>
              ) : (
                <ul className="mt-3 flex flex-col gap-2">
                  {data.ratings.map((rating) => (
                    <li key={rating.assignmentId} className="text-base">
                      <span className="font-medium tabular-nums">{rating.rating}/5</span>
                      <span className="ml-2">
                        {rating.positionLabel} — {rating.eventTitle}
                      </span>
                      <span className="ml-2 text-secondary tabular-nums">
                        {formatMoment(rating.ratedAt)}
                      </span>
                      {rating.comment && (
                        <p className="mt-0.5 text-secondary">{rating.comment}</p>
                      )}
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}

function RateForm({
  mission,
  contractorId,
}: {
  mission: components['schemas']['MissionRatingView']
  contractorId: string
}) {
  const queryClient = useQueryClient()
  const [rating, setRating] = useState('4')
  const [comment, setComment] = useState('')
  const [error, setError] = useState<string | null>(null)

  const rate = useMutation({
    mutationFn: () =>
      api.post(`/network/assignments/${mission.assignmentId}/rating`, {
        rating: Number(rating),
        comment: comment || null,
      }),
    onSuccess: async () => {
      setError(null)
      await queryClient.invalidateQueries({ queryKey: ['network'] })
      await queryClient.invalidateQueries({ queryKey: ['network', contractorId] })
    },
    onError: (cause) =>
      setError(cause instanceof ApiError ? cause.message : "L'appréciation a échoué."),
  })

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault()
        rate.mutate()
      }}
      className="rounded-card border border-border p-3"
    >
      <p className="text-base">
        {mission.positionLabel} — {mission.eventTitle}
      </p>
      <p className="text-meta text-secondary tabular-nums">{formatMoment(mission.startsAt)}</p>

      <div className="mt-3 flex items-end gap-3">
        <Field id={`rating-${mission.assignmentId}`} label="Note sur 5" className="w-24">
          <Input
            id={`rating-${mission.assignmentId}`}
            type="number"
            min={1}
            max={5}
            value={rating}
            onChange={(event) => setRating(event.target.value)}
            className="h-8"
          />
        </Field>

        <Field id={`comment-${mission.assignmentId}`} label="Commentaire" className="flex-1">
          <Input
            id={`comment-${mission.assignmentId}`}
            value={comment}
            onChange={(event) => setComment(event.target.value)}
            className="h-8"
          />
        </Field>

        <Button type="submit" size="dense" disabled={rate.isPending}>
          Enregistrer
        </Button>
      </div>

      {error && (
        <p role="alert" className="mt-2 text-base text-danger">
          {error}
        </p>
      )}
    </form>
  )
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-meta font-medium text-secondary">{label}</dt>
      <dd className="mt-0.5 text-base font-medium tabular-nums">{value}</dd>
    </div>
  )
}
