import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { MessageSquareQuote, Plus, CheckCircle2, Send, RotateCcw, Lock } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import { useEmployees } from '../lib/hooks'
import type { Feedback as Fb } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, StatusPill, Badge, Avatar, Button, EmptyState } from '../ui/primitives'
import { Meter } from '../ui/widgets'
import { Modal } from '../ui/Modal'
import { Field, Input, Select, Textarea } from '../ui/form'
import { useToast } from '../ui/Toast'
import { fmtDate, tone } from '../ui/util'
import { P } from '../components/nav'

interface Category { key: string; name: string; factors: string; defaultWeight: number; canBeNa: boolean }

export default function Feedback() {
  const { has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const [open, setOpen] = useState(false)
  const canApprove = has(P.FeedbackApprove)

  const { data } = useQuery({ queryKey: ['feedback'], queryFn: async () => (await api.get<Fb[]>('/feedback')).data })
  const invalidate = () => qc.invalidateQueries({ queryKey: ['feedback'] })

  const act = useMutation({
    mutationFn: ({ id, action, reason }: { id: number; action: string; reason?: string }) =>
      api.post(`/feedback/${id}/${action}`, reason ? { reason } : {}),
    onSuccess: (_d, v) => { push(`Feedback ${v.action === 'request-revision' ? 'sent back for revision' : v.action + 'd'}`); invalidate() },
    onError: (e) => push(apiError(e), 'error'),
  })

  return (
    <div>
      <PageHeader title="Feedback" subtitle="Structured quarterly feedback with HR approval before it reaches employees" icon={<MessageSquareQuote className="h-5 w-5" />}
        actions={has(P.FeedbackSubmit) ? <Button icon={<Plus className="h-4 w-4" />} onClick={() => setOpen(true)}>New feedback</Button> : undefined} />

      {data?.length ? (
        <div className="space-y-3">
          {data.map((f) => {
            const avg = f.categoryScores.filter((c) => !c.notApplicable)
            const mean = avg.length ? Math.round(avg.reduce((a, c) => a + c.score, 0) / avg.length) : 0
            return (
              <Card key={f.id} className="p-5">
                <div className="flex flex-wrap items-start gap-4">
                  <Avatar name={f.subjectName} color={f.avatarColor} size={46} />
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2 flex-wrap">
                      <p className="font-bold">{f.subjectName}</p>
                      <Badge t="info">{f.period}</Badge>
                      {f.projectName && <Badge t="brand">{f.projectName}</Badge>}
                      <StatusPill value={f.status} />
                    </div>
                    <p className="text-xs text-slate-500 mt-0.5">By {f.authorName} · {fmtDate(f.createdAt)}</p>
                    <p className="text-sm mt-2">{f.constructiveSummary}</p>
                    {f.revisionReason && <p className="text-sm mt-1 text-amber-600">Revision requested: {f.revisionReason}</p>}

                    {f.categoryScores.length > 0 && (
                      <div className="mt-3 grid sm:grid-cols-2 gap-x-6 gap-y-2 max-w-2xl">
                        {f.categoryScores.map((c) => (
                          <div key={c.category} className="flex items-center gap-2">
                            <span className="text-xs text-slate-500 w-40 truncate">{c.categoryName}</span>
                            {c.notApplicable ? <Badge t="neutral">N/A</Badge> : <><div className="flex-1"><Meter value={c.score} tone={c.score >= 80 ? 'good' : c.score >= 70 ? 'brand' : c.score >= 60 ? 'warn' : 'danger'} /></div><span className="text-xs font-bold w-8 text-right">{c.score}</span></>}
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                  <div className="text-center shrink-0">
                    <div className={`text-2xl font-extrabold ${mean >= 80 ? 'text-emerald-600' : mean >= 70 ? 'text-brand-600' : 'text-amber-600'}`}>{mean || '—'}</div>
                    <p className="text-[10px] uppercase text-slate-400">avg score</p>
                  </div>
                </div>

                {canApprove && f.status !== 'Published' && (
                  <div className="mt-4 pt-3 border-t border-[var(--line)] flex flex-wrap gap-2 justify-end">
                    {(f.status === 'Submitted' || f.status === 'RevisionRequested') && <>
                      <Button size="sm" variant="ghost" icon={<RotateCcw className="h-4 w-4" />} onClick={() => { const reason = prompt('Reason for revision?'); if (reason) act.mutate({ id: f.id, action: 'request-revision', reason }) }}>Request revision</Button>
                      <Button size="sm" variant="secondary" icon={<CheckCircle2 className="h-4 w-4" />} onClick={() => act.mutate({ id: f.id, action: 'approve' })}>Approve</Button>
                    </>}
                    {f.status === 'Approved' && <Button size="sm" icon={<Send className="h-4 w-4" />} onClick={() => act.mutate({ id: f.id, action: 'publish' })}>Publish to employee</Button>}
                  </div>
                )}
                {f.status === 'Published' && <p className="mt-3 pt-3 border-t border-[var(--line)] text-xs text-emerald-600 inline-flex items-center gap-1"><CheckCircle2 className="h-3.5 w-3.5" />Published to {f.subjectName} on {fmtDate(f.publishedAt)}</p>}
              </Card>
            )
          })}
        </div>
      ) : <Card><EmptyState icon={<MessageSquareQuote className="h-6 w-6" />} title="No feedback yet" hint="Submit structured feedback; HR approves before employees see it." /></Card>}

      <NewFeedbackModal open={open} onClose={() => setOpen(false)} onDone={() => { invalidate(); setOpen(false) }} push={push} />
    </div>
  )
}

function NewFeedbackModal({ open, onClose, onDone, push }: any) {
  const employees = useEmployees()
  const projects = useQuery({ queryKey: ['projects', ''], queryFn: async () => (await api.get('/projects', { params: { pageSize: 50 } })).data.items as any[] })
  const cats = useQuery({ queryKey: ['perf-cats'], queryFn: async () => (await api.get('/performance/categories')).data.categories as Category[], enabled: open })

  const [form, setForm] = useState<any>({ subjectUserId: 0, projectId: 0, period: '2026-Q3', constructiveSummary: '', strengths: '', improvementAreas: '', actionPlan: '', internalNotes: '', submitNow: true })
  const [scores, setScores] = useState<Record<string, { score: number; na: boolean }>>({})

  const submit = useMutation({
    mutationFn: () => api.post('/feedback', {
      ...form, projectId: form.projectId || null,
      categoryScores: (cats.data ?? []).map((c) => ({ category: c.key, score: scores[c.key]?.score ?? 75, notApplicable: scores[c.key]?.na ?? false, comment: '' })),
    }),
    onSuccess: () => { push(form.submitNow ? 'Feedback submitted for HR approval' : 'Draft saved'); onDone() },
    onError: (e: any) => push(apiError(e), 'error'),
  })

  return (
    <Modal open={open} onClose={onClose} size="lg" title="New structured feedback" subtitle="Scores are indicators; HR approves before the employee sees anything"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={submit.isPending} disabled={!form.subjectUserId || !form.constructiveSummary} onClick={() => submit.mutate()}>{form.submitNow ? 'Submit for approval' : 'Save draft'}</Button></>}>
      <div className="space-y-4">
        <div className="grid sm:grid-cols-3 gap-4">
          <Field label="Employee" required><Select value={form.subjectUserId} onChange={(e) => setForm({ ...form, subjectUserId: Number(e.target.value) })}><option value={0}>Select…</option>{employees.data?.map((e) => <option key={e.id} value={e.id}>{e.name}</option>)}</Select></Field>
          <Field label="Project"><Select value={form.projectId} onChange={(e) => setForm({ ...form, projectId: Number(e.target.value) })}><option value={0}>None</option>{projects.data?.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</Select></Field>
          <Field label="Period"><Input value={form.period} onChange={(e) => setForm({ ...form, period: e.target.value })} placeholder="2026-Q3" /></Field>
        </div>

        <div>
          <p className="av-label mb-2">Competency scores (0–100)</p>
          <div className="space-y-2.5">
            {cats.data?.map((c) => {
              const s = scores[c.key] ?? { score: 75, na: false }
              return (
                <div key={c.key} className="flex items-center gap-3">
                  <span className="text-sm w-52 truncate" title={c.factors}>{c.name}</span>
                  <input type="range" min={0} max={100} value={s.score} disabled={s.na}
                    onChange={(e) => setScores({ ...scores, [c.key]: { ...s, score: Number(e.target.value) } })}
                    className="flex-1 accent-brand-500 disabled:opacity-40" />
                  <span className="text-sm font-bold w-8 text-right">{s.na ? '—' : s.score}</span>
                  {c.canBeNa && <label className="text-xs flex items-center gap-1 text-slate-500"><input type="checkbox" checked={s.na} onChange={(e) => setScores({ ...scores, [c.key]: { ...s, na: e.target.checked } })} />N/A</label>}
                </div>
              )
            })}
          </div>
        </div>

        <div className="grid sm:grid-cols-2 gap-4">
          <Field label="Constructive summary" required><Textarea value={form.constructiveSummary} onChange={(e) => setForm({ ...form, constructiveSummary: e.target.value })} /></Field>
          <Field label="Strengths"><Textarea value={form.strengths} onChange={(e) => setForm({ ...form, strengths: e.target.value })} /></Field>
          <Field label="Improvement areas"><Textarea value={form.improvementAreas} onChange={(e) => setForm({ ...form, improvementAreas: e.target.value })} /></Field>
          <Field label="Action plan"><Textarea value={form.actionPlan} onChange={(e) => setForm({ ...form, actionPlan: e.target.value })} /></Field>
        </div>
        <Field label="Internal notes (never shown to the employee)"><Textarea value={form.internalNotes} onChange={(e) => setForm({ ...form, internalNotes: e.target.value })} /></Field>
        <label className="flex items-center gap-2 text-sm font-medium"><input type="checkbox" checked={form.submitNow} onChange={(e) => setForm({ ...form, submitNow: e.target.checked })} /> Submit for HR approval now (uncheck to save as draft)</label>
      </div>
    </Modal>
  )
}
