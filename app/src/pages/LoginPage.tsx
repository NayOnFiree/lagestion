import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Navigate, useNavigate } from 'react-router'
import * as z from 'zod'
import { Button } from '@/components/ui/button'
import { Field } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { ApiError } from '@/lib/api'
import { useAuth } from '@/lib/auth-context'

const schema = z.object({
  agencySlug: z.string().trim().min(1, 'Indiquez le code de votre agence'),
  email: z.email('Adresse électronique invalide'),
  password: z.string().min(1, 'Saisissez votre mot de passe'),
})

type LoginForm = z.infer<typeof schema>

export function LoginPage() {
  const { user, signIn } = useAuth()
  const navigate = useNavigate()
  const [formError, setFormError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginForm>({
    resolver: zodResolver(schema),
    defaultValues: { agencySlug: '', email: '', password: '' },
  })

  if (user) {
    return <Navigate to="/" replace />
  }

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)

    try {
      await signIn(values)
      await navigate('/', { replace: true })
    } catch (error) {
      setFormError(
        error instanceof ApiError && error.status === 401
          ? 'Agence, adresse ou mot de passe incorrect.'
          : 'Connexion impossible pour le moment. Réessayez dans un instant.',
      )
    }
  })

  return (
    <div className="flex min-h-dvh flex-col justify-center px-4 py-8">
      <div className="mb-8">
        <h1 className="text-title font-semibold">LaGestion</h1>
        <p className="mt-1 text-base text-secondary">Connectez-vous pour voir vos missions</p>
      </div>

      <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
        <Field id="agencySlug" label="Code agence" error={errors.agencySlug?.message}>
          <Input
            id="agencySlug"
            autoCapitalize="none"
            autoCorrect="off"
            autoComplete="organization"
            aria-invalid={Boolean(errors.agencySlug)}
            {...register('agencySlug')}
          />
        </Field>

        <Field id="email" label="Adresse électronique" error={errors.email?.message}>
          <Input
            id="email"
            type="email"
            inputMode="email"
            autoCapitalize="none"
            autoComplete="username"
            aria-invalid={Boolean(errors.email)}
            {...register('email')}
          />
        </Field>

        <Field id="password" label="Mot de passe" error={errors.password?.message}>
          <Input
            id="password"
            type="password"
            autoComplete="current-password"
            aria-invalid={Boolean(errors.password)}
            {...register('password')}
          />
        </Field>

        {formError && (
          <p role="alert" className="text-base text-danger">
            {formError}
          </p>
        )}

        <Button type="submit" size="block" disabled={isSubmitting} className="mt-2">
          {isSubmitting ? 'Connexion…' : 'Se connecter'}
        </Button>
      </form>
    </div>
  )
}
