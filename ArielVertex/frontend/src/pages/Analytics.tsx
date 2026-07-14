import { useQuery } from '@tanstack/react-query'
import { PieChart, Download, Users, CheckCircle2 } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { PageHeader } from '../components/PageHeader'
import { Card, Badge, EmptyState, Button } from '../ui/primitives'
import { useToast } from '../ui/Toast'

interface Analytics {
  pmsCompletion: { total: number; completed: number; pending: number; completionRate: number }
  ratingDistribution: { label: string; count: number }[]
  departmentPerformance: { department: string; rating: number; count: number }[]
  promotionReport: { stage: string; count: number }[]
  trainingNeeds: { status: string; count: number }[]
  trainingByGap: { skillGap: string; count: number }[]
  talentMatrix: { name: string; department: string; performance: number; potential: number; quadrant: string }[]
}

const QUADRANTS = [
  { key: 'High Potential', tone: 'warn' as const, hint: 'High potential · developing performance' },
  { key: 'Star', tone: 'good' as const, hint: 'High performance · high potential' },
  { key: 'Needs Attention', tone: 'danger' as const, hint: 'Developing on both axes' },
  { key: 'Core Performer', tone: 'info' as const, hint: 'Strong performance · steady potential' },
]

export default function Analytics() {
  const { push } = useToast()
  const { data, isLoading } = useQuery({ queryKey: ['analytics'], queryFn: async () => (await api.get<Analytics>('/analytics')).data })

  const exportPayroll = async () => {
    try {
      const res = await api.get('/analytics/payroll-export', { responseType: 'blob' })
      const url = URL.createObjectURL(res.data as Blob)
      const a = document.createElement('a'); a.href = url; a.download = 'payroll-revisions.csv'; a.click()
      URL.revokeObjectURL(url)
    } catch (e) { push(apiError(e), 'error') }
  }

  const maxRating = Math.max(1, ...(data?.ratingDistribution.map((r) => r.count) ?? [1]))
  return (
    <div>
      <PageHeader title="Talent Analytics" subtitle="Appraisal completion, rating spread, department performance and the 9-box matrix" icon={<PieChart className="h-5 w-5" />}
        actions={<Button variant="secondary" icon={<Download className="h-4 w-4" />} onClick={exportPayroll}>Payroll CSV</Button>} />

      {isLoading ? <Card><p className="p-6 text-sm text-slate-500">Loading analytics…</p></Card> : !data ? <Card><EmptyState icon={<PieChart className="h-6 w-6" />} title="No analytics available" /></Card> : (
        <div className="space-y-4">
          <div className="grid gap-3 sm:grid-cols-3">
            <Card className="p-4"><p className="text-xs text-slate-500">Completion rate</p><p className="text-2xl font-bold">{data.pmsCompletion.completionRate}%</p></Card>
            <Card className="p-4"><p className="text-xs text-slate-500 inline-flex items-center gap-1"><CheckCircle2 className="h-3.5 w-3.5" />Completed</p><p className="text-2xl font-bold">{data.pmsCompletion.completed}</p></Card>
            <Card className="p-4"><p className="text-xs text-slate-500 inline-flex items-center gap-1"><Users className="h-3.5 w-3.5" />Pending</p><p className="text-2xl font-bold">{data.pmsCompletion.pending}</p></Card>
          </div>

          <div className="grid gap-4 lg:grid-cols-2">
            <Card className="p-5">
              <p className="font-bold mb-3">Rating distribution</p>
              <div className="space-y-2">
                {data.ratingDistribution.map((r) => (
                  <div key={r.label} className="flex items-center gap-3 text-sm">
                    <span className="w-44 shrink-0 text-slate-500 text-xs">{r.label}</span>
                    <div className="flex-1 h-3 rounded-full bg-slate-100 dark:bg-navy-600 overflow-hidden"><div className="h-full bg-brand-500 rounded-full" style={{ width: `${(r.count / maxRating) * 100}%` }} /></div>
                    <span className="w-6 text-right font-semibold">{r.count}</span>
                  </div>
                ))}
              </div>
            </Card>
            <Card className="p-5">
              <p className="font-bold mb-3">Department performance</p>
              {data.departmentPerformance.length ? (
                <div className="space-y-2">
                  {data.departmentPerformance.map((d) => (
                    <div key={d.department} className="flex items-center gap-3 text-sm">
                      <span className="w-36 shrink-0 truncate">{d.department}</span>
                      <div className="flex-1 h-3 rounded-full bg-slate-100 dark:bg-navy-600 overflow-hidden"><div className="h-full bg-emerald-500 rounded-full" style={{ width: `${(d.rating / 5) * 100}%` }} /></div>
                      <span className="w-10 text-right font-semibold">{d.rating.toFixed(1)}</span>
                    </div>
                  ))}
                </div>
              ) : <p className="text-sm text-slate-500">No released ratings yet.</p>}
            </Card>
          </div>

          <Card className="p-5">
            <p className="font-bold mb-1">9-box talent matrix</p>
            <p className="text-xs text-slate-500 mb-3">Performance (rating) × potential (goal progress)</p>
            <div className="grid grid-cols-2 gap-3">
              {QUADRANTS.map((qd) => {
                const people = data.talentMatrix.filter((p) => p.quadrant === qd.key)
                return (
                  <div key={qd.key} className="rounded-xl border border-[var(--line)] p-3 min-h-[110px]">
                    <div className="flex items-center gap-2 mb-2"><Badge t={qd.tone}>{qd.key}</Badge><span className="text-[10px] text-slate-400">{people.length}</span></div>
                    <div className="flex flex-wrap gap-1.5">
                      {people.map((p, i) => <span key={i} className="text-xs rounded-lg bg-slate-100 dark:bg-navy-600 px-2 py-0.5">{p.name} · {p.performance.toFixed(1)}</span>)}
                      {!people.length && <span className="text-xs text-slate-400">{qd.hint}</span>}
                    </div>
                  </div>
                )
              })}
            </div>
          </Card>

          <div className="grid gap-4 lg:grid-cols-2">
            <Card className="p-5">
              <p className="font-bold mb-3">Promotion pipeline</p>
              <div className="flex flex-wrap gap-2">
                {data.promotionReport.map((s) => <Badge key={s.stage} t="info">{s.stage.replace(/([a-z])([A-Z])/g, '$1 $2')}: {s.count}</Badge>)}
              </div>
            </Card>
            <Card className="p-5">
              <p className="font-bold mb-3">Top skill gaps</p>
              {data.trainingByGap.length ? (
                <div className="flex flex-wrap gap-2">{data.trainingByGap.map((g) => <Badge key={g.skillGap} t="warn">{g.skillGap}: {g.count}</Badge>)}</div>
              ) : <p className="text-sm text-slate-500">No training data yet.</p>}
            </Card>
          </div>
        </div>
      )}
    </div>
  )
}
