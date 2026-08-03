import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import * as z from 'zod'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent } from '@/components/ui/dialog'
import { Field } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { api, ApiError } from '@/lib/api'
import { fromLocalInput, toLocalInput } from '@/lib/events'
import type { components } from '@/types/api'

type PositionDetail = components['schemas']['PositionDetail']
type SavedPosition = components['schemas']['SavedPosition']

const schema = z
  .object({
    label: z.string().trim().min(1, "L'intitulé est obligatoire"),
    headcount: z.coerce.number().int().min(1, 'Au moins une personne'),
    startsAt: z.string().min(1, 'Indiquez le début'),
    endsAt: z.string().min(1, 'Indiquez la fin'),
    hourlyRate: z.coerce.number().min(0, 'Tarif invalide'),
    dressCode: z.string().trim(),
    brief: z.string().trim(),
  })
  .refine((values) => new Date(values.endsAt) > new Date(values.startsAt), {
    message: 'La fin doit suivre le début',
    path: ['endsAt'],
  })

type PositionForm = z.input<typeof schema>

export function PositionDialog({
  eventId,
  existing,
  eventStartsAt,
  open,
  onOpenChange,
}: {
  eventId: string
  existing?: PositionDetail
  eventStartsAt: string
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const queryClient = useQueryClient()
  const [formError, setFormError] = useState<string | null>(null)
  const [impacted, setImpacted] = useState<string[]>([])

  const { register, handleSubmit, formState } = useForm<PositionForm>({
    resolver: zodResolver(schema),
    defaultValues: existing
      ? {
          label: existing.label,
          headcount: existing.headcount,
          startsAt: toLocalInput(existing.startsAt),
          endsAt: toLocalInput(existing.endsAt),
          hourlyRate: existing.hourlyRate,
          dressCode: existing.dressCode ?? '',
          brief: existing.brief ?? '',
        }
      : {
          label: '',
          headcount: 1,
          startsAt: toLocalInput(eventStartsAt),
          endsAt: toLocalInput(new Date(new Date(eventStartsAt).getTime() + 6 * 3_600_000).toISOString()),
          hourlyRate: 0,
          dressCode: '',
          brief: '',
        },
  })

  const save = useMutation({
    mutationFn: (values: PositionForm) => {
      const body = {
        label: values.label,
        headcount: Number(values.headcount),
        startsAt: fromLocalInput(values.startsAt),
        endsAt: fromLocalInput(values.endsAt),
        hourlyRate: Number(values.hourlyRate),
        dressCode: values.dressCode || null,
        brief: values.brief || null,
      }

      return existing
        ? api.put<SavedPosition>(`/positions/${existing.id}`, body)
        : api.post<SavedPosition>(`/events/${eventId}/positions`, body)
    },
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: ['event', eventId] })
      await queryClient.invalidateQueries({ queryKey: ['events'] })

      // On ne ferme pas tant que l'agence n'a pas vu qui elle doit prévenir.
      if (saved.impactedContractors.length > 0) {
        setImpacted(saved.impactedContractors)
        return
      }

      onOpenChange(false)
    },
    onError: (cause) =>
      setFormError(
        cause instanceof ApiError ? cause.message : "L'enregistrement a échoué. Réessayez.",
      ),
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent title={existing ? 'Modifier le poste' : 'Nouveau poste'}>
        {impacted.length > 0 ? (
          <div>
            <p className="text-base text-warning">
              Le tarif ou les horaires ont changé après acceptation.
            </p>
            <p className="mt-2 text-base text-secondary">
              À prévenir : {impacted.join(', ')}. Ces prestataires ont accepté des conditions
              différentes.
            </p>
            <div className="mt-4 flex justify-end">
              <Button type="button" onClick={() => onOpenChange(false)}>
                J'ai compris
              </Button>
            </div>
          </div>
        ) : (
          <form
            onSubmit={handleSubmit((values) => save.mutate(values))}
            noValidate
            className="flex flex-col gap-4"
          >
            <Field id="label" label="Intitulé du poste" error={formState.errors.label?.message}>
              <Input id="label" {...register('label')} />
            </Field>

            <div className="grid grid-cols-2 gap-4">
              <Field
                id="headcount"
                label="Effectif recherché"
                error={formState.errors.headcount?.message}
              >
                <Input id="headcount" type="number" min={1} {...register('headcount')} />
              </Field>

              <Field
                id="hourlyRate"
                label="Tarif horaire"
                hint="En euros, hors taxes"
                error={formState.errors.hourlyRate?.message}
              >
                <Input id="hourlyRate" type="number" step="0.5" min={0} {...register('hourlyRate')} />
              </Field>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <Field id="posStartsAt" label="Début" error={formState.errors.startsAt?.message}>
                <Input id="posStartsAt" type="datetime-local" {...register('startsAt')} />
              </Field>

              <Field id="posEndsAt" label="Fin" error={formState.errors.endsAt?.message}>
                <Input id="posEndsAt" type="datetime-local" {...register('endsAt')} />
              </Field>
            </div>

            <Field id="dressCode" label="Tenue exigée" error={formState.errors.dressCode?.message}>
              <Input id="dressCode" {...register('dressCode')} />
            </Field>

            <Field
              id="brief"
              label="Brief"
              hint="Ce qu'il y a à faire, transmis avec la proposition"
              error={formState.errors.brief?.message}
            >
              <Input id="brief" {...register('brief')} />
            </Field>

            {formError && (
              <p role="alert" className="text-base text-danger">
                {formError}
              </p>
            )}

            <div className="mt-2 flex justify-end gap-2">
              <Button type="button" variant="ghost" onClick={() => onOpenChange(false)}>
                Annuler
              </Button>
              <Button type="submit" disabled={save.isPending}>
                {save.isPending ? 'Enregistrement…' : 'Enregistrer'}
              </Button>
            </div>
          </form>
        )}
      </DialogContent>
    </Dialog>
  )
}
