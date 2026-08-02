import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { components } from '../types/api'

/** Type généré depuis le Swagger de l'API (`npm run gen:api`). */
type HealthResponse = components['schemas']['HealthResponse']

/**
 * Preuve que la chaîne front → API → PostgreSQL fonctionne de bout en bout.
 */
export function StatusPage() {
  const { data, error, isPending, isFetching, refetch } = useQuery({
    queryKey: ['health'],
    queryFn: () => api.get<HealthResponse>('/health'),
    retry: false,
  })

  return (
    <section>
      <h2 className="text-lg font-semibold">Statut de l'API</h2>
      <p className="mt-1 text-sm text-slate-500">
        Appel de <code className="rounded bg-slate-200 px-1">GET /health</code>.
      </p>

      <div className="mt-4 rounded-xl border border-slate-200 bg-white p-4">
        {isPending && <p className="text-sm text-slate-500">Vérification…</p>}

        {error && (
          <div>
            <p className="font-medium text-red-600">API injoignable</p>
            <p className="mt-1 text-sm text-slate-500">{error.message}</p>
          </div>
        )}

        {data && (
          <dl className="space-y-2 text-sm">
            <div className="flex items-center justify-between">
              <dt className="text-slate-500">Statut</dt>
              <dd className="font-medium">{data.status}</dd>
            </div>
            <div className="flex items-center justify-between">
              <dt className="text-slate-500">Base de données</dt>
              <dd className="font-medium">{data.database ? 'connectée' : 'injoignable'}</dd>
            </div>
            <div className="flex items-center justify-between">
              <dt className="text-slate-500">Vérifié à</dt>
              <dd className="font-medium tabular-nums">
                {new Date(data.timestamp).toLocaleTimeString('fr-FR')}
              </dd>
            </div>
          </dl>
        )}
      </div>

      <button
        type="button"
        onClick={() => void refetch()}
        disabled={isFetching}
        className="mt-4 h-12 w-full rounded-xl bg-slate-900 font-medium text-white disabled:opacity-50"
      >
        {isFetching ? 'Vérification…' : 'Revérifier'}
      </button>
    </section>
  )
}
