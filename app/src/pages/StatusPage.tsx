import { useQuery } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
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
      <h2 className="text-title font-semibold">Statut de l'API</h2>
      <p className="mt-1 text-base text-secondary">Vérification de la connexion au serveur.</p>

      <Card className="mt-4">
        {isPending && <div className="h-12 rounded-control bg-surface" aria-busy="true" />}

        {error && (
          <CardHeader>
            <CardTitle className="text-danger">Serveur injoignable</CardTitle>
            <CardDescription>{error.message}</CardDescription>
          </CardHeader>
        )}

        {data && (
          <dl className="flex flex-col gap-3">
            <Row label="Statut" value={data.status === 'healthy' ? 'en service' : 'dégradé'} />
            <Row label="Base de données" value={data.database ? 'connectée' : 'injoignable'} />
            <Row
              label="Vérifié à"
              value={new Date(data.timestamp).toLocaleTimeString('fr-FR', {
                hour: '2-digit',
                minute: '2-digit',
              })}
            />
          </dl>
        )}
      </Card>

      <Button
        type="button"
        variant="outline"
        size="block"
        onClick={() => void refetch()}
        disabled={isFetching}
        className="mt-4"
      >
        {isFetching ? 'Vérification…' : 'Revérifier'}
      </Button>
    </section>
  )
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline justify-between gap-4">
      <dt className="text-base text-secondary">{label}</dt>
      <dd className="text-base font-medium">{value}</dd>
    </div>
  )
}
