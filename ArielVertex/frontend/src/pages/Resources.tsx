import { useQuery } from '@tanstack/react-query'
import { GaugeCircle } from 'lucide-react'
import { api } from '../lib/api'
import { PageHeader } from '../components/PageHeader'
import { Card, CardHeader, Badge, Avatar, EmptyState, Skeleton } from '../ui/primitives'
import { Meter } from '../ui/widgets'
import { Tone } from '../ui/util'

const availTone = (a: string): Tone => a === 'Free' ? 'info' : a === 'Partially Available' ? 'brand' : a === 'Fully Allocated' ? 'good' : 'danger'

export default function Resources() {
  const { data, isLoading } = useQuery({ queryKey: ['occupancy'], queryFn: async () => (await api.get('/resources/occupancy')).data as any })

  return (
    <div>
      <PageHeader title="Resource Visibility" subtitle="Allocation across projects — free, partial, allocated, overloaded" icon={<GaugeCircle className="h-5 w-5" />} />

      {data && (
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 mb-4">
          {data.buckets.map((b: any) => (
            <Card key={b.bucket} className="p-4">
              <p className="text-xs font-semibold uppercase text-slate-500">{b.bucket}</p>
              <p className="text-2xl font-extrabold mt-1">{b.count}</p>
            </Card>
          ))}
        </div>
      )}

      <Card>
        <CardHeader title="People" subtitle="Total allocation and per-project breakdown" />
        <div className="p-5 space-y-3">
          {isLoading ? [...Array(5)].map((_, i) => <Skeleton key={i} className="h-14" />)
            : data?.rows.length ? data.rows.map((r: any) => (
              <div key={r.userId} className="flex flex-wrap items-center gap-4 rounded-xl border border-[var(--line)] p-3.5">
                <Avatar name={r.name} color={r.avatarColor} size={42} />
                <div className="min-w-0 flex-1">
                  <p className="font-semibold text-sm">{r.name}</p>
                  <p className="text-xs text-slate-500">{r.designation}</p>
                  <div className="mt-1.5 flex flex-wrap gap-1.5">
                    {r.projects.map((p: any) => <Badge key={p.projectId} t="info">{p.projectName} · {p.allocationPct}%</Badge>)}
                    {r.projects.length === 0 && <span className="text-xs text-slate-400">No active allocation</span>}
                  </div>
                </div>
                <div className="w-40">
                  <div className="flex justify-between text-xs mb-1"><span className="text-slate-500">Load</span><span className="font-bold">{r.totalAllocationPct}%</span></div>
                  <Meter value={r.totalAllocationPct} tone={availTone(r.availability)} />
                </div>
                <Badge t={availTone(r.availability)}>{r.availability}</Badge>
              </div>
            )) : <EmptyState icon={<GaugeCircle className="h-6 w-6" />} title="No allocation data" />}
        </div>
      </Card>
    </div>
  )
}
