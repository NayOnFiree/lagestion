import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useNavigate } from 'react-router'
import * as z from 'zod'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent } from '@/components/ui/dialog'
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
import { fromLocalInput, toLocalInput } from '@/lib/events'
import type { components } from '@/types/api'

type EventDetail = components['schemas']['EventDetail']

const schema = z
  .object({
    title: z.string().trim().min(1, "L'intitulé est obligatoire"),
    clientName: z.string().trim(),
    isConfidential: z.boolean(),
    startsAt: z.string().min(1, 'Indiquez le début'),
    endsAt: z.string().min(1, 'Indiquez la fin'),
    address: z.string().trim(),
    accessNotes: z.string().trim(),
    status: z.string().min(1),
  })
  .refine((values) => new Date(values.endsAt) > new Date(values.startsAt), {
    message: 'La fin doit suivre le début',
    path: ['endsAt'],
  })

type EventForm = z.infer<typeof schema>

function defaults(existing?: EventDetail): EventForm {
  if (!existing) {
    const start = new Date()
    start.setHours(start.getHours() + 24, 0, 0, 0)
    const end = new Date(start.getTime() + 6 * 3_600_000)

    return {
      title: '',
      clientName: '',
      isConfidential: false,
      startsAt: toLocalInput(start.toISOString()),
      endsAt: toLocalInput(end.toISOString()),
      address: '',
      accessNotes: '',
      status: 'Draft',
    }
  }

  return {
    title: existing.title,
    clientName: existing.clientName ?? '',
    isConfidential: existing.isConfidential,
    startsAt: toLocalInput(existing.startsAt),
    endsAt: toLocalInput(existing.endsAt),
    address: existing.address ?? '',
    accessNotes: existing.accessNotes ?? '',
    status: existing.status,
  }
}

export function EventDialog({
  open,
  onOpenChange,
  existing,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  existing?: EventDetail
}) {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const [formError, setFormError] = useState<string | null>(null)

  const { register, handleSubmit, control, formState } = useForm<EventForm>({
    resolver: zodResolver(schema),
    defaultValues: defaults(existing),
  })

  const save = useMutation({
    mutationFn: (values: EventForm) => {
      const body = {
        title: values.title,
        clientName: values.clientName || null,
        isConfidential: values.isConfidential,
        startsAt: fromLocalInput(values.startsAt),
        endsAt: fromLocalInput(values.endsAt),
        address: values.address || null,
        accessNotes: values.accessNotes || null,
        status: values.status,
      }

      return existing
        ? api.put<EventDetail>(`/events/${existing.id}`, body)
        : api.post<EventDetail>('/events', body)
    },
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: ['events'] })
      onOpenChange(false)

      if (!existing) {
        void navigate(`/evenements/${saved.id}`)
      }
    },
    onError: (cause) =>
      setFormError(
        cause instanceof ApiError ? cause.message : "L'enregistrement a échoué. Réessayez.",
      ),
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        title={existing ? "Modifier l'événement" : 'Nouvel événement'}
        description={
          existing
            ? undefined
            : 'Une fois créé, découpez-le en postes pour pouvoir proposer des missions.'
        }
      >
        <form
          onSubmit={handleSubmit((values) => save.mutate(values))}
          noValidate
          className="flex flex-col gap-4"
        >
          <Field id="title" label="Intitulé" error={formState.errors.title?.message}>
            <Input id="title" {...register('title')} />
          </Field>

          <div className="grid grid-cols-2 gap-4">
            <Field id="clientName" label="Client" error={formState.errors.clientName?.message}>
              <Input id="clientName" {...register('clientName')} />
            </Field>

            <Field id="status" label="Statut" error={formState.errors.status?.message}>
              <Controller
                control={control}
                name="status"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger id="status">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Draft">brouillon</SelectItem>
                      <SelectItem value="Published">publié</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
            </Field>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <Field id="startsAt" label="Début" error={formState.errors.startsAt?.message}>
              <Input id="startsAt" type="datetime-local" {...register('startsAt')} />
            </Field>

            <Field id="endsAt" label="Fin" error={formState.errors.endsAt?.message}>
              <Input id="endsAt" type="datetime-local" {...register('endsAt')} />
            </Field>
          </div>

          <Field id="address" label="Lieu" error={formState.errors.address?.message}>
            <Input id="address" {...register('address')} />
          </Field>

          <Field
            id="accessNotes"
            label="Modalités d'accès"
            hint="Quai, badge, code — transmis aux prestataires retenus"
            error={formState.errors.accessNotes?.message}
          >
            <Input id="accessNotes" {...register('accessNotes')} />
          </Field>

          <label className="flex items-center gap-2 text-base">
            <input type="checkbox" className="size-4 accent-accent" {...register('isConfidential')} />
            Masquer le nom du client aux prestataires
          </label>

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
      </DialogContent>
    </Dialog>
  )
}
