import { useQuery } from '@tanstack/react-query'
import { LineChart as LChart, Line, XAxis, YAxis, ResponsiveContainer, Tooltip, CartesianGrid } from 'recharts'
import { LineChart, TrendingUp, Award, Target, ListChecks, Lock, FileDown } from 'lucide-react'
import { api } from '../lib/api'
import type { MyPerformance as MyPerf, Feedback } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, CardHeader, Badge, EmptyState, Skeleton, Button } from '../ui/primitives'
import { ScoreRing, Meter } from '../ui/widgets'
import { fmtDate } from '../ui/util'

export default function MyPerformance() {
  const { data, isLoading } = useQuery({ queryKey: ['my-perf'], queryFn: async () => (await api.get<MyPerf>('/performance/my')).data })
  const feedback = useQuery({ queryKey: ['my-feedback'], queryFn: async () => (await api.get<Feedback[]>('/feedback/my-published')).data })

  if (isLoading) return <div className="space-y-4"><Skeleton className="h-10 w-64" /><Skeleton className="h-72" /></div>

  const latest = data?.latest
  return (
    <div>
      <PageHeader title="My Performance" subtitle="Your approved performance dashboard — indicators, not automatic judgement" icon={<LineChart className="h-5 w-5" />} />

      {!data?.hasPublishedReport ? (
        <Card><EmptyState icon={<Lock className="h-6 w-6" />} title="No published performance yet" hint="Your performance summary appears here once HR reviews and publishes it." /></Card>
      ) : latest && (
        <>
          <div className="grid lg:grid-cols-3 gap-4">
            <Card className="p-6 flex flex-col items-center justify-center text-center">
              <ScoreRing score={latest.overallScore} />
              <Badge t={latest.overallScore >= 80 ? 'good' : latest.overallScore >= 70 ? 'brand' : 'warn'} className="mt-3">{latest.ratingLabel}</Badge>
              <p className="text-xs text-slate-400 mt-2">{latest.period} · {latest.periodType}</p>
              <Button variant="secondary" size="sm" className="mt-4" icon={<FileDown className="h-4 w-4" />} onClick={() => window.open(`/report/${latest.id}`, '_blank')}>Save as PDF</Button>
            </Card>

            <Card className="lg:col-span-2">
              <CardHeader title="Category breakdown" subtitle="Weighted contribution to your score" icon={<Award className="h-[18px] w-[18px]" />} />
              <div className="p-5 space-y-3">
                {latest.categories.map((c) => (
                  <div key={c.category} className="flex items-center gap-3">
                    <span className="text-sm w-52 truncate">{c.categoryName}</span>
                    {c.notApplicable ? <Badge t="neutral">N/A</Badge> : <>
                      <div className="flex-1"><Meter value={c.score} tone={c.score >= 80 ? 'good' : c.score >= 70 ? 'brand' : c.score >= 60 ? 'warn' : 'danger'} /></div>
                      <span className="text-sm font-bold w-10 text-right">{c.score}</span>
                      <span className="text-xs text-slate-400 w-12 text-right">{c.weight}%</span>
                    </>}
                  </div>
                ))}
              </div>
            </Card>
          </div>

          <div className="grid lg:grid-cols-3 gap-4 mt-4">
            <Card className="lg:col-span-2">
              <CardHeader title="Trend" subtitle="Your monthly performance over time" icon={<TrendingUp className="h-[18px] w-[18px]" />} />
              <div className="p-5 h-64">
                <ResponsiveContainer width="100%" height="100%">
                  <LChart data={data.trend}>
                    <CartesianGrid strokeDasharray="3 3" className="stroke-slate-200 dark:stroke-navy-600" vertical={false} />
                    <XAxis dataKey="period" tick={{ fontSize: 12 }} stroke="#94a3b8" />
                    <YAxis domain={[0, 100]} tick={{ fontSize: 12 }} stroke="#94a3b8" />
                    <Tooltip />
                    <Line type="monotone" dataKey="score" stroke="#1E7FD4" strokeWidth={3} dot={{ r: 4, fill: '#1E7FD4' }} activeDot={{ r: 6 }} />
                  </LChart>
                </ResponsiveContainer>
              </div>
            </Card>

            <div className="space-y-4">
              <Card className="p-5">
                <p className="av-label mb-1 inline-flex items-center gap-1.5"><Award className="h-3.5 w-3.5 text-emerald-500" />Strengths</p>
                <p className="text-sm">{latest.strengths || '—'}</p>
              </Card>
              <Card className="p-5">
                <p className="av-label mb-1 inline-flex items-center gap-1.5"><Target className="h-3.5 w-3.5 text-amber-500" />Improvement areas</p>
                <p className="text-sm">{latest.improvementAreas || '—'}</p>
              </Card>
              <Card className="p-5">
                <p className="av-label mb-1 inline-flex items-center gap-1.5"><ListChecks className="h-3.5 w-3.5 text-brand-500" />Recommended actions</p>
                <p className="text-sm">{latest.recommendedActions || '—'}</p>
              </Card>
            </div>
          </div>
        </>
      )}

      {feedback.data && feedback.data.length > 0 && (
        <Card className="mt-4">
          <CardHeader title="Published feedback" subtitle="Constructive feedback approved by HR" />
          <div className="p-5 space-y-3">
            {feedback.data.map((f) => (
              <div key={f.id} className="rounded-xl border border-[var(--line)] p-4">
                <div className="flex items-center gap-2 mb-1"><Badge t="info">{f.period}</Badge>{f.projectName && <Badge t="brand">{f.projectName}</Badge>}</div>
                <p className="text-sm">{f.constructiveSummary}</p>
                {f.strengths && <p className="text-sm mt-1"><span className="font-semibold text-emerald-600">Strengths:</span> {f.strengths}</p>}
                {f.improvementAreas && <p className="text-sm mt-1"><span className="font-semibold text-amber-600">Focus:</span> {f.improvementAreas}</p>}
                {f.actionPlan && <p className="text-sm mt-1"><span className="font-semibold text-brand-600">Plan:</span> {f.actionPlan}</p>}
              </div>
            ))}
          </div>
        </Card>
      )}
    </div>
  )
}
