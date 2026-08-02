import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'

export function Input({ className, type, ...props }: ComponentProps<'input'>) {
  return (
    <input
      type={type}
      className={cn(
        'h-11 w-full rounded-control border border-border bg-bg px-3',
        'text-base text-primary placeholder:text-muted',
        'outline-none focus-visible:border-accent focus-visible:ring-1 focus-visible:ring-accent',
        'disabled:opacity-50',
        'aria-invalid:border-danger aria-invalid:focus-visible:ring-danger',
        className,
      )}
      {...props}
    />
  )
}
