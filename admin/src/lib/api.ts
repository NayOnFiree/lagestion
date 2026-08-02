/**
 * Client HTTP centralisé.
 *
 * L'access token vit en mémoire, jamais dans localStorage ni sessionStorage :
 * une injection de script ne peut pas le lire. Sa persistance entre deux
 * rechargements est assurée par le cookie de rafraîchissement, httpOnly, que
 * le JavaScript ne voit pas non plus.
 */

const baseUrl = import.meta.env.VITE_API_URL

if (!baseUrl) {
  throw new Error(
    "VITE_API_URL n'est pas défini. Copiez .env.example vers .env puis relancez le serveur de dev.",
  )
}

/** Corps d'erreur renvoyé par l'API (RFC 9457, via ProblemDetails). */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
}

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | null

  constructor(status: number, problem: ProblemDetails | null) {
    super(problem?.detail ?? problem?.title ?? `Requête échouée (HTTP ${status})`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

let accessToken: string | null = null
let onSessionLost: (() => void) | null = null

export function setAccessToken(token: string | null) {
  accessToken = token
}

/** Appelé quand la session ne peut plus être rétablie : le contexte d'auth déconnecte. */
export function setSessionLostHandler(handler: (() => void) | null) {
  onSessionLost = handler
}

/** Chemins qui portent eux-mêmes l'authentification et ne doivent pas être rejoués. */
const authPaths = ['/auth/login', '/auth/refresh', '/auth/logout']

/**
 * Session rétablie à partir du cookie de rafraîchissement.
 *
 * Le refresh token est à usage unique : deux appels concurrents présenteraient
 * le même jeton, et le second serait pris pour un rejeu — ce qui révoque toute
 * la chaîne côté serveur. Les appelants simultanés partagent donc une seule
 * requête en vol. C'est notamment le cas au montage sous StrictMode, qui
 * exécute les effets deux fois.
 */
let inFlightRefresh: Promise<RefreshedSession | null> | null = null

interface RefreshedSession {
  accessToken: string
  expiresInSeconds: number
  user: unknown
}

export function refreshSession(): Promise<RefreshedSession | null> {
  inFlightRefresh ??= (async () => {
    try {
      const response = await fetch(`${baseUrl}/auth/refresh`, {
        method: 'POST',
        credentials: 'include',
        headers: { Accept: 'application/json' },
      })

      if (!response.ok) {
        return null
      }

      const payload = (await response.json()) as RefreshedSession
      accessToken = payload.accessToken
      return payload
    } catch {
      return null
    } finally {
      // Libéré au tour de boucle suivant, le temps que les appelants
      // simultanés aient récupéré la même promesse.
      queueMicrotask(() => {
        inFlightRefresh = null
      })
    }
  })()

  return inFlightRefresh
}

async function send(path: string, init: RequestInit): Promise<Response> {
  return fetch(`${baseUrl}${path}`, {
    ...init,
    credentials: 'include',
    headers: {
      Accept: 'application/json',
      // Pas de Content-Type sur un FormData : le navigateur doit poser
      // lui-même le multipart avec sa frontière.
      ...(typeof init.body === 'string' ? { 'Content-Type': 'application/json' } : {}),
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      ...init.headers,
    },
  })
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  let response = await send(path, init)

  // Jeton expiré : on tente une rotation, puis on rejoue une seule fois.
  if (response.status === 401 && !authPaths.includes(path)) {
    if (await refreshSession()) {
      response = await send(path, init)
    } else {
      onSessionLost?.()
    }
  }


  const payload = response.status === 204 ? null : await response.json().catch(() => null)

  if (!response.ok) {
    throw new ApiError(response.status, payload as ProblemDetails | null)
  }

  return payload as T
}

export const api = {
  get: <T>(path: string, init?: RequestInit) => request<T>(path, { ...init, method: 'GET' }),
  post: <T>(path: string, body?: unknown, init?: RequestInit) =>
    request<T>(path, { ...init, method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) }),
  /** Envoi multipart, pour le dépôt de fichiers. */
  postForm: <T>(path: string, form: FormData) => request<T>(path, { method: 'POST', body: form }),
  put: <T>(path: string, body?: unknown, init?: RequestInit) =>
    request<T>(path, { ...init, method: 'PUT', body: JSON.stringify(body) }),
  delete: <T>(path: string, init?: RequestInit) => request<T>(path, { ...init, method: 'DELETE' }),
}
