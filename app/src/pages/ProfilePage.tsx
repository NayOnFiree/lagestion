import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import * as z from 'zod'
import { Button } from '@/components/ui/button'
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
import { legalStatusLabels } from '@/lib/labels'
import type { components } from '@/types/api'

type ContractorProfile = components['schemas']['ContractorProfile']

const schema = z.object({
  firstName: z.string().trim().min(1, 'Indiquez votre prénom'),
  lastName: z.string().trim().min(1, 'Indiquez votre nom'),
  phone: z.string().trim(),
  legalStatus: z.string().min(1, 'Choisissez un statut juridique'),
  siret: z
    .string()
    .trim()
    .refine((value) => value === '' || /^\d{14}$/.test(value.replace(/\s/g, '')), {
      message: 'Le SIRET compte 14 chiffres',
    }),
  address: z.string().trim(),
  iban: z.string().trim(),
  defaultHourlyRate: z
    .string()
    .refine((value) => value === '' || Number(value.replace(',', '.')) >= 0, {
      message: 'Tarif invalide',
    }),
  baseCity: z.string().trim(),
  travelRadiusKm: z
    .string()
    .refine((value) => value === '' || Number(value) >= 0, { message: 'Rayon invalide' }),
})

type ProfileForm = z.infer<typeof schema>

function toForm(profile: ContractorProfile): ProfileForm {
  return {
    firstName: profile.firstName,
    lastName: profile.lastName,
    phone: profile.phone ?? '',
    legalStatus: profile.legalStatus,
    siret: profile.siret ?? '',
    address: profile.address ?? '',
    iban: profile.iban ?? '',
    defaultHourlyRate: profile.defaultHourlyRate?.toString() ?? '',
    baseCity: profile.baseCity ?? '',
    travelRadiusKm: profile.travelRadiusKm?.toString() ?? '',
  }
}

export function ProfilePage() {
  const { data, isPending, isError } = useQuery({
    queryKey: ['profile'],
    queryFn: () => api.get<ContractorProfile>('/me/profile'),
  })

  if (isPending) {
    return <ProfileSkeleton />
  }

  if (isError || !data) {
    return (
      <section>
        <h2 className="text-title font-semibold">Mon profil</h2>
        <p className="mt-1 text-base text-danger">
          Votre fiche n'a pas pu être chargée. Réessayez dans un instant.
        </p>
      </section>
    )
  }

  // Le formulaire n'est monté qu'une fois la fiche connue : ses champs
  // démarrent alors avec leur valeur définitive. Le monter vide puis le
  // remplir laisserait les composants de sélection en mode non contrôlé,
  // et ils ignoreraient la valeur posée après coup.
  return <ProfileForm profile={data} />
}

function ProfileForm({ profile }: { profile: ContractorProfile }) {
  const queryClient = useQueryClient()
  const [saved, setSaved] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  const { register, handleSubmit, control, reset, formState } = useForm<ProfileForm>({
    resolver: zodResolver(schema),
    defaultValues: toForm(profile),
  })

  const save = useMutation({
    mutationFn: (values: ProfileForm) =>
      api.put<ContractorProfile>('/me/profile', {
        firstName: values.firstName,
        lastName: values.lastName,
        phone: values.phone || null,
        legalStatus: values.legalStatus,
        siret: values.siret.replace(/\s/g, '') || null,
        address: values.address || null,
        iban: values.iban.replace(/\s/g, '') || null,
        defaultHourlyRate: values.defaultHourlyRate
          ? Number(values.defaultHourlyRate.replace(',', '.'))
          : null,
        baseCity: values.baseCity || null,
        travelRadiusKm: values.travelRadiusKm ? Number(values.travelRadiusKm) : null,
      }),
    onSuccess: async (updated) => {
      setFormError(null)
      setSaved(true)
      reset(toForm(updated))
      await queryClient.invalidateQueries({ queryKey: ['profile'] })
      await queryClient.invalidateQueries({ queryKey: ['documents'] })
    },
    onError: (error) => {
      setSaved(false)
      setFormError(
        error instanceof ApiError && error.status === 400
          ? "Certaines informations n'ont pas été acceptées. Vérifiez le SIRET et l'IBAN."
          : "L'enregistrement a échoué. Réessayez dans un instant.",
      )
    },
  })

  return (
    <section>
      <h2 className="text-title font-semibold">Mon profil</h2>
      <p className="mt-1 text-base text-secondary">
        Ces informations alimentent vos factures. Gardez-les à jour.
      </p>

      <form
        onSubmit={handleSubmit((values) => save.mutate(values))}
        noValidate
        className="mt-6 flex flex-col gap-4"
      >
        <Field id="firstName" label="Prénom" error={formState.errors.firstName?.message}>
          <Input id="firstName" autoComplete="given-name" {...register('firstName')} />
        </Field>

        <Field id="lastName" label="Nom" error={formState.errors.lastName?.message}>
          <Input id="lastName" autoComplete="family-name" {...register('lastName')} />
        </Field>

        <Field id="phone" label="Téléphone" error={formState.errors.phone?.message}>
          <Input id="phone" type="tel" inputMode="tel" autoComplete="tel" {...register('phone')} />
        </Field>

        <Field
          id="legalStatus"
          label="Statut juridique"
          error={formState.errors.legalStatus?.message}
        >
          <Controller
            control={control}
            name="legalStatus"
            render={({ field }) => (
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger id="legalStatus">
                  <SelectValue placeholder="Choisissez" />
                </SelectTrigger>
                <SelectContent>
                  {Object.entries(legalStatusLabels).map(([value, label]) => (
                    <SelectItem key={value} value={value}>
                      {label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
        </Field>

        <Field
          id="siret"
          label="SIRET"
          hint="14 chiffres, sans espace"
          error={formState.errors.siret?.message}
        >
          <Input id="siret" inputMode="numeric" {...register('siret')} />
        </Field>

        <Field id="address" label="Adresse" error={formState.errors.address?.message}>
          <Input id="address" autoComplete="street-address" {...register('address')} />
        </Field>

        <Field
          id="iban"
          label="IBAN"
          hint="Utilisé pour vos règlements"
          error={formState.errors.iban?.message}
        >
          <Input id="iban" autoCapitalize="characters" {...register('iban')} />
        </Field>

        <Field
          id="defaultHourlyRate"
          label="Tarif horaire par défaut"
          hint="En euros, hors taxes"
          error={formState.errors.defaultHourlyRate?.message}
        >
          <Input id="defaultHourlyRate" inputMode="decimal" {...register('defaultHourlyRate')} />
        </Field>

        <Field
          id="baseCity"
          label="Ville de rattachement"
          error={formState.errors.baseCity?.message}
        >
          <Input id="baseCity" {...register('baseCity')} />
        </Field>

        <Field
          id="travelRadiusKm"
          label="Rayon de déplacement"
          hint="En kilomètres"
          error={formState.errors.travelRadiusKm?.message}
        >
          <Input id="travelRadiusKm" inputMode="numeric" {...register('travelRadiusKm')} />
        </Field>

        {formError && (
          <p role="alert" className="text-base text-danger">
            {formError}
          </p>
        )}

        {/* Filet : sans lui, une erreur sur un champ dont le message ne
            s'affiche pas bloquerait l'envoi sans rien expliquer. */}
        {formState.submitCount > 0 && Object.keys(formState.errors).length > 0 && (
          <p role="alert" className="text-base text-danger">
            Corrigez les champs signalés avant d'enregistrer.
          </p>
        )}

        {saved && !formState.isDirty && (
          <p role="status" className="text-base text-success">
            Profil enregistré.
          </p>
        )}

        <Button type="submit" size="block" disabled={save.isPending} className="mt-2">
          {save.isPending ? 'Enregistrement…' : 'Enregistrer'}
        </Button>
      </form>
    </section>
  )
}

function ProfileSkeleton() {
  return (
    <section aria-busy="true">
      <div className="h-7 w-40 rounded-control bg-surface" />
      <div className="mt-6 flex flex-col gap-4">
        {Array.from({ length: 6 }, (_, index) => (
          <div key={index} className="flex flex-col gap-1.5">
            <div className="h-4 w-24 rounded-control bg-surface" />
            <div className="h-11 w-full rounded-control bg-surface" />
          </div>
        ))}
      </div>
    </section>
  )
}
