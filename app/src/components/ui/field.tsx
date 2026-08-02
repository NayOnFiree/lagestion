import type { ReactNode } from 'react'
import { Label } from '@/components/ui/label'
import { cn } from '@/lib/utils'

/**
 * Champ de formulaire : label au-dessus, message d'erreur en dessous en 12px
 * danger. Jamais de placeholder à la place d'un label.
 */
export function Field({
  id,
  label,
  error,
  hint,
  children,
  className,
}: {
  id: string
  label: string
  error?: string
  hint?: string
  children: ReactNode
  className?: string
}) {
  return (
    <div className={cn('flex flex-col gap-1.5', className)}>
      <Label htmlFor={id}>{label}</Label>
      {children}
      {hint && !error && <p className="text-meta text-secondary">{hint}</p>}
      {error && (
        <p id={`${id}-error`} className="text-meta text-danger">
          {error}
        </p>
      )}
    </div>
  )
}
