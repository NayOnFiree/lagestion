/**
 * Libellés français des énumérations de l'API.
 *
 * L'API renvoie des valeurs stables en anglais ; l'UI est en français, sans
 * majuscule à chaque mot et sans point final.
 */

export const legalStatusLabels: Record<string, string> = {
  AutoEntrepreneur: 'auto-entrepreneur',
  EntrepriseIndividuelle: 'entreprise individuelle',
  Eurl: 'EURL',
  Sasu: 'SASU',
  Sarl: 'SARL',
  Other: 'autre',
}

export const documentTypeLabels: Record<string, string> = {
  IdentityCard: "pièce d'identité",
  UrssafCertificate: 'attestation de vigilance URSSAF',
  CompanyRegistration: 'Kbis ou avis SIRENE',
  LiabilityInsurance: 'attestation RC pro',
  BankDetails: 'RIB',
  DrivingLicence: 'permis de conduire',
  Certification: 'habilitation',
  Other: 'autre',
}

export const documentStatusLabels: Record<string, string> = {
  Pending: 'en attente de validation',
  Approved: 'validée',
  Rejected: 'refusée',
}

export const profileFieldLabels: Record<string, string> = {
  Siret: 'SIRET',
  Iban: 'IBAN',
}

/** Date en toutes lettres : jamais 14/03/2026. */
export function formatDate(value: string | null | undefined) {
  if (!value) {
    return null
  }

  return new Date(`${value}T00:00:00`).toLocaleDateString('fr-FR', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  })
}

/** Jours de la semaine, dans l'ordre français : lundi d'abord. */
export const weekdays = [
  { value: 'Monday', short: 'lun.' },
  { value: 'Tuesday', short: 'mar.' },
  { value: 'Wednesday', short: 'mer.' },
  { value: 'Thursday', short: 'jeu.' },
  { value: 'Friday', short: 'ven.' },
  { value: 'Saturday', short: 'sam.' },
  { value: 'Sunday', short: 'dim.' },
] as const

/** « jeu. 14 mars », jamais 14/03/2026. */
export function formatDayLabel(isoDate: string) {
  return new Date(`${isoDate}T00:00:00`).toLocaleDateString('fr-FR', {
    weekday: 'short',
    day: 'numeric',
    month: 'long',
  })
}

/** « 18h » ou « 18h30 » : on n'écrit pas 18:00. */
export function formatTime(time: string | null | undefined) {
  if (!time) {
    return null
  }

  const [hours, minutes] = time.split(':')
  return minutes === '00' ? `${Number(hours)}h` : `${Number(hours)}h${minutes}`
}

export function formatSlot(startsAt?: string | null, endsAt?: string | null) {
  const start = formatTime(startsAt)
  const end = formatTime(endsAt)

  return start && end ? `${start}–${end}` : 'journée entière'
}

export function formatAmount(amount: number) {
  return amount.toLocaleString('fr-FR', { style: 'currency', currency: 'EUR' })
}

export function formatFileSize(bytes: number) {
  return bytes < 1024 * 1024
    ? `${Math.max(1, Math.round(bytes / 1024))} ko`
    : `${(bytes / (1024 * 1024)).toFixed(1)} Mo`
}
