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
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-lg font-semibold">Statut de l'API</h1>
          <p className="mt-1 text-sm text-slate-500">
            Appel de <code className="rounded bg-slate-200 px-1">GET /health</code>.
          </p>
        </div>
        <button
          type="button"
          onClick={() => void refetch()}
          disabled={isFetching}
          className="rounded border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium hover:bg-slate-50 disabled:opacity-50"
        >
          {isFetching ? 'Vérification…' : 'Revérifier'}
        </button>
      </div>

      <div className="mt-4 overflow-x-auto rounded border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
            <tr>
              <th className="px-3 py-2 font-medium">Contrôle</th>
              <th className="px-3 py-2 font-medium">Valeur</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {isPending && (
              <tr>
                <td className="px-3 py-2 text-slate-500" colSpan={2}>
                  Vérification…
                </td>
              </tr>
            )}

            {error && (
              <tr>
                <td className="px-3 py-2 font-medium text-red-600">API injoignable</td>
                <td className="px-3 py-2 text-slate-500">{error.message}</td>
              </tr>
            )}

            {data && (
              <>
                <tr>
                  <td className="px-3 py-2 text-slate-500">Statut</td>
                  <td className="px-3 py-2 font-medium">{data.status}</td>
                </tr>
                <tr>
                  <td className="px-3 py-2 text-slate-500">Base de données</td>
                  <td className="px-3 py-2 font-medium">
                    {data.database ? 'connectée' : 'injoignable'}
                  </td>
                </tr>
                <tr>
                  <td className="px-3 py-2 text-slate-500">Vérifié à</td>
                  <td className="px-3 py-2 font-medium tabular-nums">
                    {new Date(data.timestamp).toLocaleTimeString('fr-FR')}
                  </td>
                </tr>
              </>
            )}
          </tbody>
        </table>
      </div>
    </section>
  )
}
