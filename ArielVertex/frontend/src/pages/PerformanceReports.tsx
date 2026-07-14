import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Gauge, Sparkles, Send, CheckCircle2, Database, Info, FileDown } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import { useEmployees } from '../lib/hooks'
import type { PerformanceReport } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, CardHeader, Badge, Avatar, Button, EmptyState, Skeleton } from '../ui/primitives'
import { Meter } from '../ui/widgets'
import { Field, Select, Input } from '../ui/form'
import { useToast } from '../ui/Toast'
import { fmtDate, Tone } from '../ui/util'
import { P } from '../components/nav'

const scoreTone = (s: number): Tone => s >= 80 ? 'good' : s >= 70 ? 'brand' : s >= 60 ? 'warn' : 'danger'

export default function PerformanceReports() {
  const { has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const employees = useEmployees()
  const canPublish = has(P.PerformancePublish)

  const { data, isLoading } = useQuery({ queryKey: ['perf-reports'], queryFn: async () => (await api.get<PerformanceReport[]>('/performance/reports')).data })
  const invalidate = () => { qc.invalidateQueries({ queryKey: ['perf-reports'] }); qc.invalidateQueries({ queryKey: ['my-perf'] }) }

  const [form, setForm] = useState({ subjectUserId: 0, periodType: 'Quarterly', period: '2026-Q2' })
  const [generated, setGenerated] = useState<PerformanceReport | null>(null)

  const generate = useMutation({
    mutationFn: () => api.post<PerformanceReport>('/performance/generate', form),
    onSuccess: (r) => { push('Report generated from evidence — review, then publish'); setGenerated(r.data); invalidate() },
    onError: (e) => push(apiError(e), 'error'),
  })
  const publish = useMutation({
    mutationFn: (id: number) => api.post(`/performance/${id}/publish`),
    onSuccess: () => { push('Published — the employee can now see it'); setGenerated(null); invalidate() },
    onError: (e) => push(apiError(e), 'error'),
  })

  return (
    <div>
      <PageHeader title="Performance Reports" subtitle="Generate transparent scores from real evidence, then approve for the employee" icon={<Gauge className="h-5 w-5" />} />

      <div className="grid lg:grid-cols-5 gap-4">
        {/* Generator */}
        <Card className="lg:col-span-2 h-fit">
          <CardHeader title="Generate a report" subtitle="Rolls up status, reviews & feedback" icon={<Sparkles className="h-[18px] w-[18px]" />} />
          <div className="p-5 space-y-4">
            <Field label="Employee" required>
              <Select value={form.subjectUserId} onChange={(e) => setForm({ ...form, subjectUserId: Number(e.target.value) })}>
                <option value={0}>Select an employee…</option>
                {employees.data?.map((e) => <option key={e.id} value={e.id}>{e.name} — {e.designation}</option>)}
              </Select>
            </Field>
            <div className="grid grid-cols-2 gap-3">
              <Field label="Period type"><Select value={form.periodType} onChange={(e) => setForm({ ...form, periodType: e.target.value, period: e.target.value === 'Monthly' ? '2026-06' : '2026-Q2' })}><option value="Quarterly">Quarterly</option><option value="Monthly">Monthly</option></Select></Field>
              <Field label="Period" hint={form.periodType === 'Monthly' ? 'YYYY-MM' : 'YYYY-Qn'}><Input value={form.period} onChange={(e) => setForm({ ...form, period: e.target.value })} /></Field>
            </div>
            <Button className="w-full" loading={generate.isPending} disabled={!form.subjectUserId} onClick={() => generate.mutate()} icon={<Sparkles className="h-4 w-4" />}>Generate report</Button>
            <p className="text-xs text-slate-400 inline-flex items-start gap-1.5"><Info className="h-3.5 w-3.5 mt-0.5 shrink-0" />Scores are indicators computed from evidence. Nothing reaches the employee until you publish.</p>
          </div>

          {generated && (
            <div className="mx-5 mb-5 rounded-xl border-2 border-brand-200 dark:border-brand-800 p-4">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2"><Avatar name={generated.subjectName} size={30} /><span className="font-bold text-sm">{generated.subjectName}</span></div>
                <Badge t="warn">Draft</Badge>
              </div>
              <div className="flex items-baseline gap-2 mt-2">
                <span className={`text-3xl font-extrabold ${generated.overallScore >= 80 ? 'text-emerald-600' : 'text-brand-600'}`}>{generated.overallScore}</span>
                <span className="text-sm text-slate-500">/ 100 · {generated.ratingLabel}</span>
              </div>
              <p className="text-xs text-slate-500 mt-1 inline-flex items-center gap-1.5"><Database className="h-3.5 w-3.5" />{generated.dataSources}</p>
              {canPublish && <Button size="sm" className="w-full mt-3" icon={<Send className="h-4 w-4" />} loading={publish.isPending} onClick={() => publish.mutate(generated.id)}>Approve &amp; publish</Button>}
            </div>
          )}
        </Card>

        {/* Reports list */}
        <Card className="lg:col-span-3">
          <CardHeader title="All reports" subtitle="Drafts and published, newest first" />
          <div className="p-5 space-y-3">
            {isLoading ? [...Array(4)].map((_, i) => <Skeleton key={i} className="h-24" />)
              : data?.length ? data.map((r) => (
                <div key={r.id} className="rounded-xl border border-[var(--line)] p-4">
                  <div className="flex flex-wrap items-center gap-3">
                    <Avatar name={r.subjectName} size={40} />
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2 flex-wrap">
                        <p className="font-bold">{r.subjectName}</p>
                        <Badge t="info">{r.period}</Badge>
                        {r.isPublished ? <Badge t="good"><CheckCircle2 className="h-3.5 w-3.5" />Published</Badge> : <Badge t="warn">Draft</Badge>}
                      </div>
                      <p className="text-xs text-slate-400 mt-0.5">{r.dataSources}</p>
                    </div>
                    <div className="text-center">
                      <div className={`text-2xl font-extrabold ${r.overallScore >= 80 ? 'text-emerald-600' : r.overallScore >= 70 ? 'text-brand-600' : 'text-amber-600'}`}>{r.overallScore}</div>
                      <p className="text-[10px] uppercase text-slate-400">{r.ratingLabel}</p>
                    </div>
                    <Button variant="secondary" size="sm" icon={<FileDown className="h-4 w-4" />} onClick={() => window.open(`/report/${r.id}`, '_blank')}>PDF</Button>
                    {canPublish && !r.isPublished && <Button size="sm" icon={<Send className="h-4 w-4" />} onClick={() => publish.mutate(r.id)}>Publish</Button>}
                  </div>
                  <div className="mt-3 grid sm:grid-cols-2 gap-x-6 gap-y-2">
                    {r.categories.map((c) => (
                      <div key={c.category} className="flex items-center gap-2">
                        <span className="text-xs text-slate-500 w-40 truncate">{c.categoryName}</span>
                        {c.notApplicable ? <Badge t="neutral">N/A</Badge> : <><div className="flex-1"><Meter value={c.score} tone={scoreTone(c.score)} /></div><span className="text-xs font-bold w-8 text-right">{c.score}</span></>}
                      </div>
                    ))}
                  </div>
                </div>
              )) : <EmptyState icon={<Gauge className="h-6 w-6" />} title="No reports yet" hint="Generate a report from the panel on the left." />}
          </div>
        </Card>
      </div>
    </div>
  )
}
