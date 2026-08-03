import { cva, type VariantProps } from 'class-variance-authority'
import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'

const badgeVariants = cva(
  'inline-flex items-center rounded-control px-2 py-0.5 text-meta font-medium whitespace-nowrap',
  {
    variants: {
      // Les statuts ne sont jamais décoratifs : une seule teinte par sens.
      tone: {
        neutral: 'bg-surface text-secondary',
        accent: 'bg-accent-weak text-accent',
        warning: 'bg-bg text-warning',
        danger: 'bg-bg text-danger',
      },
    },
    defaultVariants: {
      tone: 'neutral',
    },
  },
)

export function Badge({
  className,
  tone,
  ...props
}: ComponentProps<'span'> & VariantProps<typeof badgeVariants>) {
  return <span className={cn(badgeVariants({ tone }), className)} {...props} />
}
