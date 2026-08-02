import { useQuery } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { api } from '@/lib/api'
import type { components } from '@/types/api'

type HealthResponse = components['schemas']['HealthResponse']

/** Preuve que la chaîne front → API → PostgreSQL fonctionne de bout en bout. */
export function StatusPage() {
  const { data, error, isPending, isFetching, refetch } = useQuery({
    queryKey: ['health'],
    queryFn: () => api.get<HealthResponse>('/health'),
    retry: false,
  })

  return (
    <section>
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-title font-semibold">Statut de l'API</h1>
          <p className="mt-1 text-base text-secondary">Vérification de la connexion au serveur.</p>
        </div>
        <Button
          type="button"
          variant="outline"
          size="dense"
          onClick={() => void refetch()}
          disabled={isFetching}
        >
          {isFetching ? 'Vérification…' : 'Revérifier'}
        </Button>
      </div>

      <div className="mt-4 overflow-x-auto rounded-card border border-border">
        <table className="w-full text-left text-dense">
          <thead className="bg-surface text-meta font-medium text-secondary">
            <tr>
              <th className="h-row px-3">Contrôle</th>
              <th className="h-row px-3">Valeur</th>
            </tr>
          </thead>
          <tbody>
            {isPending && (
              <tr className="border-t border-border">
                <td className="h-row px-3 text-secondary" colSpan={2}>
                  Vérification…
                </td>
              </tr>
            )}

            {error && (
              <tr className="border-t border-border">
                <td className="h-row px-3 font-medium text-danger">Serveur injoignable</td>
                <td className="h-row px-3 text-secondary">{error.message}</td>
              </tr>
            )}

            {data && (
              <>
                <Row label="Statut" value={data.status === 'healthy' ? 'en service' : 'dégradé'} />
                <Row
                  label="Base de données"
                  value={data.database ? 'connectée' : 'injoignable'}
                />
                <Row
                  label="Vérifié à"
                  value={new Date(data.timestamp).toLocaleTimeString('fr-FR')}
                />
              </>
            )}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <tr className="border-t border-border">
      <td className="h-row px-3 text-secondary">{label}</td>
      <td className="h-row px-3 font-medium">{value}</td>
    </tr>
  )
}
