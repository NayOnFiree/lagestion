import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

/** Fusionne des classes Tailwind en laissant la dernière gagner. */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}
