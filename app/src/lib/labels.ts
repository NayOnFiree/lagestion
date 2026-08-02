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

export function formatFileSize(bytes: number) {
  return bytes < 1024 * 1024
    ? `${Math.max(1, Math.round(bytes / 1024))} ko`
    : `${(bytes / (1024 * 1024)).toFixed(1)} Mo`
}
