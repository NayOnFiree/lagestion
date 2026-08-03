import * as DialogPrimitive from '@radix-ui/react-dialog'
import { X } from 'lucide-react'
import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'

export const Dialog = DialogPrimitive.Root
export const DialogTrigger = DialogPrimitive.Trigger
export const DialogClose = DialogPrimitive.Close

export function DialogContent({
  className,
  children,
  title,
  description,
  ...props
}: ComponentProps<typeof DialogPrimitive.Content> & { title: string; description?: string }) {
  return (
    <DialogPrimitive.Portal>
      <DialogPrimitive.Overlay className="fixed inset-0 z-50 bg-primary/20" />
      <DialogPrimitive.Content
        className={cn(
          'fixed top-1/2 left-1/2 z-50 w-[calc(100vw-32px)] max-w-2xl -translate-x-1/2 -translate-y-1/2',
          'max-h-[calc(100vh-64px)] overflow-y-auto',
          // Élément flottant : c'est l'un des rares cas où l'ombre est admise.
          'rounded-modal border border-border bg-bg p-6 shadow-floating',
          className,
        )}
        {...props}
      >
        <div className="flex items-start justify-between gap-4">
          <div>
            <DialogPrimitive.Title className="text-strong font-medium">{title}</DialogPrimitive.Title>
            {description && (
              <DialogPrimitive.Description className="mt-1 text-base text-secondary">
                {description}
              </DialogPrimitive.Description>
            )}
          </div>
          <DialogPrimitive.Close
            aria-label="Fermer"
            className="rounded-control p-1 text-secondary outline-none hover:bg-surface focus-visible:ring-2 focus-visible:ring-accent"
          >
            <X className="size-4" aria-hidden />
          </DialogPrimitive.Close>
        </div>

        <div className="mt-4">{children}</div>
      </DialogPrimitive.Content>
    </DialogPrimitive.Portal>
  )
}
