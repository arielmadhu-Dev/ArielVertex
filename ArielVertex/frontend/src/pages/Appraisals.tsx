import { useEffect, useMemo, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Award, Send, CheckCircle2, Star, UserRound, ShieldCheck, Scale, Info } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import type { Appraisal, Cycle } from '../lib/types'
import {
  useAppraisalForm, useAppraisalScores, localScore, band, bandTone,
  type AreaScoreInput, type FormVariant,
} from '../lib/appraisalForms'
import { PageHeader } from '../components/PageHeader'
import { Card, Badge, StatusPill, Avatar, Button, EmptyState, Skeleton } from '../ui/primitives'
import { Rating1to5, RatingLegend } from '../ui/RatingScale'
import { Modal } from '../ui/Modal'
import { Select, Textarea } from '../ui/form'
import { useToast } from '../ui/Toast'
import { cx } from '../ui/util'
import { P } from '../components/nav'

type Action = { appraisal: Appraisal; kind: 'self' | 'manager' | 'release' }

const STAGES = [
  { key: 'self', label: 'Self Assessment', who: 'Employee' },
  { key: 'manager', label: 'Manager Evaluation', who: 'Reporting Manager' },
  { key: 'release', label: 'HR Release', who: 'Final rating' },
]
const stageIndex = (stage: Appraisal['stage']) =>
  stage === 'SelfPending' ? 0 : stage === 'SelfSubmitted' ? 1 : stage === 'ManagerCompleted' ? 2 : 3

export default function Appraisals() {
  const { user, has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const [cycleId, setCycleId] = useState<number | 0>(0)
  const [action, setAction] = useState<Action | null>(null)

  const cycles = useQuery({ queryKey: ['cycles'], queryFn: async () => (await api.get<Cycle[]>('/cycles')).data })
  const { data, isLoading } = useQuery({
    queryKey: ['appraisals', cycleId],
    queryFn: async () => (await api.get<Appraisal[]>('/appraisals', { params: cycleId ? { cycleId } : {} })).data,
  })
  const invalidate = () => qc.invalidateQueries({ queryKey: ['appraisals'] })

  const canRelease = has(P.AppraisalsRelease)
  const canManage = has(P.AppraisalsManage) || canRelease

  return (
    <div>
      <PageHeader title="Appraisals" subtitle="Self assessment → manager evaluation → HR releases the final rating" icon={<Award className="h-5 w-5" />}
        actions={
          <Select value={cycleId} onChange={(e) => setCycleId(Number(e.target.value))} className="w-52">
            <option value={0}>All cycles</option>
            {cycles.data?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </Select>
        } />

      {isLoading ? <div className="space-y-3">{[...Array(4)].map((_, i) => <Skeleton key={i} className="h-24" />)}</div>
        : data?.length ? (
          <div className="space-y-3">
            {data.map((a) => {
              const isOwner = a.employeeId === user?.id
              const isManager = a.managerId === user?.id
              const doSelf = isOwner && a.stage === 'SelfPending'
              const doManager = (isManager || canManage) && a.stage === 'SelfSubmitted'
              const doRelease = canRelease && a.stage === 'ManagerCompleted'
              const step = stageIndex(a.stage)
              return (
                <Card key={a.id} className="p-4">
                  <div className="flex flex-wrap items-center gap-4">
                    <Avatar name={a.employeeName} color={a.avatarColor} size={42} />
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2 flex-wrap">
                        <p className="font-bold">{a.employeeName}</p>
                        {a.employeeRole && <Badge t="brand">{a.employeeRole}</Badge>}
                        <StatusPill value={a.stage} />
                        <Badge t="info">{a.cycleName}</Badge>
                      </div>
                      <p className="text-xs text-slate-500 mt-0.5">
                        {a.managerName ? `Manager: ${a.managerName}` : 'No manager assigned'}
                        {a.selfRating != null && ` · Self ${a.selfRating.toFixed(1)}`}
                        {a.managerRating != null && ` · Manager ${a.managerRating.toFixed(1)}`}
                      </p>
                    </div>
                    <div className="flex items-center gap-2">
                      {a.stage === 'Released' && (
                        <span className="inline-flex items-center gap-1 text-emerald-600 text-sm font-semibold">
                          <CheckCircle2 className="h-4 w-4" />{a.finalRating?.toFixed(1)} / 5
                        </span>
                      )}
                      {doSelf && <Button size="sm" icon={<Send className="h-4 w-4" />} onClick={() => setAction({ appraisal: a, kind: 'self' })}>Submit self</Button>}
                      {doManager && <Button size="sm" icon={<Star className="h-4 w-4" />} onClick={() => setAction({ appraisal: a, kind: 'manager' })}>Evaluate</Button>}
                      {doRelease && <Button size="sm" variant="secondary" icon={<CheckCircle2 className="h-4 w-4" />} onClick={() => setAction({ appraisal: a, kind: 'release' })}>Release</Button>}
                      {!doSelf && !doManager && !doRelease && a.stage !== 'Released' && (
                        <Button size="sm" variant="ghost" onClick={() => setAction({ appraisal: a, kind: isOwner ? 'self' : 'manager' })}>View</Button>
                      )}
                    </div>
                  </div>
                  <Stepper current={step} className="mt-3" />
                </Card>
              )
            })}
          </div>
        ) : <Card><EmptyState icon={<Award className="h-6 w-6" />} title="No appraisals" hint="Appraisals appear once HR activates a cycle." /></Card>}

      {action && <AppraisalModal action={action} onClose={() => setAction(null)} onDone={() => { invalidate(); setAction(null) }} push={push} />}
    </div>
  )
}

function Stepper({ current, className }: { current: number; className?: string }) {
  return (
    <div className={cx('flex items-center gap-2', className)}>
      {STAGES.map((s, i) => (
        <div key={s.key} className="flex items-center gap-2 flex-1 last:flex-none">
          <div className="flex items-center gap-2 min-w-0">
            <span className={cx('grid place-items-center h-6 w-6 rounded-full text-[11px] font-bold shrink-0',
              i < current ? 'bg-emerald-500 text-white'
                : i === current ? 'bg-brand-500 text-white ring-4 ring-brand-100 dark:ring-brand-900/50'
                  : 'bg-slate-100 dark:bg-navy-600 text-slate-400')}>
              {i < current ? '✓' : i + 1}
            </span>
            <span className={cx('text-[11px] font-semibold truncate hidden sm:block',
              i === current ? 'text-brand-600 dark:text-brand-300' : 'text-slate-400')}>{s.label}</span>
          </div>
          {i < STAGES.length - 1 && <span className={cx('h-0.5 flex-1 rounded-full', i < current ? 'bg-emerald-400' : 'bg-slate-200 dark:bg-navy-600')} />}
        </div>
      ))}
    </div>
  )
}

function AppraisalModal({ action, onClose, onDone, push }: { action: Action; onClose: () => void; onDone: () => void; push: (m: string, t?: 'success' | 'error') => void }) {
  const { appraisal: a, kind } = action
  const variant: FormVariant = kind === 'self' ? 'Self' : 'Manager'
  const form = useAppraisalForm(a.employeeRole, variant, kind !== 'release')
  const scores = useAppraisalScores(kind === 'release' ? a.id : undefined)
  const priorScores = useAppraisalScores(kind === 'manager' ? a.id : undefined)

  const [answers, setAnswers] = useState<Record<string, AreaScoreInput>>({})
  const [comments, setComments] = useState('')
  const [finalRating, setFinalRating] = useState<number | null>(null)

  // Default the released rating to the blended suggestion once it loads.
  useEffect(() => {
    if (kind === 'release' && scores.data?.blendedScore != null && finalRating == null)
      setFinalRating(Number((scores.data.blendedScore / 20).toFixed(1)))
  }, [scores.data, kind, finalRating])

  const areas = form.data?.areas ?? []
  const live = useMemo(() => localScore(areas, form.data?.weighted ?? false, answers), [areas, form.data, answers])

  // What the employee rated themselves, so the manager has context.
  const selfByArea = useMemo(() => {
    const map: Record<string, number> = {}
    priorScores.data?.areas.filter((s) => s.stage === 'Self' && s.rating).forEach((s) => { map[s.areaName] = s.rating! })
    return map
  }, [priorScores.data])

  const set = (name: string, values: Partial<AreaScoreInput>) =>
    setAnswers((cur) => ({ ...cur, [name]: { ...cur[name], ...values, areaName: name } }))

  const submit = useMutation({
    mutationFn: () => {
      const payload = Object.values(answers).filter((x) => x.rating != null || x.comment || x.notApplicable)
      if (kind === 'self')
        return api.post(`/appraisals/${a.id}/self`, { selfRating: live != null ? live / 20 : 3, selfComments: comments, areas: payload })
      if (kind === 'manager')
        return api.post(`/appraisals/${a.id}/manager`, { managerRating: live != null ? live / 20 : 3, managerComments: comments, areas: payload })
      return api.post(`/appraisals/${a.id}/release`, { finalRating: finalRating ?? 3 })
    },
    onSuccess: () => {
      push(kind === 'self' ? 'Self assessment submitted — your manager has been notified'
        : kind === 'manager' ? 'Evaluation submitted — sent to HR for release'
          : 'Final rating released')
      onDone()
    },
    onError: (e) => push(apiError(e), 'error'),
  })

  const title = kind === 'self' ? 'Self assessment' : kind === 'manager' ? 'Manager evaluation' : 'Release final rating'
  const readOnly = (kind === 'self' && a.stage !== 'SelfPending') || (kind === 'manager' && a.stage !== 'SelfSubmitted')

  return (
    <Modal open onClose={onClose} size="lg" title={`${title} — ${a.employeeName}`}
      subtitle={`${a.employeeRole || 'No role set'} · ${a.cycleName}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Close</Button>
          {!readOnly && (
            <Button loading={submit.isPending} onClick={() => submit.mutate()}
              disabled={kind === 'release' ? finalRating == null : live == null}>
              {kind === 'self' ? 'Submit to manager' : kind === 'manager' ? 'Submit to HR' : 'Release rating'}
            </Button>
          )}
        </>
      }>
      <Stepper current={stageIndex(a.stage)} className="mb-5" />

      {kind === 'release' ? (
        <ReleaseView scores={scores.data} loading={scores.isLoading} finalRating={finalRating} setFinalRating={setFinalRating} />
      ) : form.isLoading ? (
        <div className="space-y-3">{[...Array(6)].map((_, i) => <Skeleton key={i} className="h-14" />)}</div>
      ) : (
        <div className="space-y-4">
          <div className="flex items-center gap-2.5 rounded-xl border border-brand-100 bg-brand-50/60 px-3.5 py-2.5 text-xs font-semibold text-brand-700 dark:border-brand-900 dark:bg-brand-900/20 dark:text-brand-200">
            {kind === 'self' ? <UserRound className="h-4 w-4 shrink-0" /> : <ShieldCheck className="h-4 w-4 shrink-0" />}
            {kind === 'self'
              ? 'Rate yourself honestly and answer the reflection questions — your manager reviews this next.'
              : `Scored on the ${a.employeeRole} scorecard.${form.data?.weighted ? ' Each area carries its own weight.' : ''}`}
          </div>

          {areas.map((area) => {
            const ans = answers[area.name]
            const na = !!ans?.notApplicable
            return (
              <div key={area.name} className="rounded-xl border border-[var(--line)] p-3.5">
                <div className="flex flex-wrap items-center gap-2 mb-2.5">
                  <span className="font-semibold text-sm flex-1 min-w-[160px]">{area.name}</span>
                  {area.type === 'Rating' && form.data?.weighted && <Badge t="brand">{area.weight}%</Badge>}
                  {selfByArea[area.name] && <Badge t="neutral">self rated {selfByArea[area.name]}</Badge>}
                  {area.allowNa && (
                    <label className="inline-flex items-center gap-1.5 text-[11px] font-semibold text-slate-500">
                      <input type="checkbox" className="h-3.5 w-3.5" checked={na} disabled={readOnly}
                        onChange={(e) => set(area.name, { notApplicable: e.target.checked, rating: null })} />
                      not applicable
                    </label>
                  )}
                </div>

                {area.type === 'Rating' ? (
                  <div className="flex flex-wrap items-center gap-3">
                    <Rating1to5 value={ans?.rating ?? null} disabled={readOnly || na}
                      onChange={readOnly ? undefined : (v) => set(area.name, { rating: v, notApplicable: false })} />
                    <Textarea value={ans?.comment ?? ''} disabled={readOnly}
                      onChange={(e) => set(area.name, { comment: e.target.value })}
                      placeholder="Comments…" className="flex-1 min-w-[180px] !min-h-[42px] !py-2 text-sm" />
                  </div>
                ) : (
                  <Textarea value={ans?.comment ?? ''} disabled={readOnly}
                    onChange={(e) => set(area.name, { comment: e.target.value })}
                    placeholder="Your answer…" className="text-sm" />
                )}
              </div>
            )
          })}

          <div className="flex flex-wrap items-center gap-4 rounded-xl border border-[var(--line)] bg-slate-50 dark:bg-navy-600/40 p-4">
            <ScoreDial score={live} />
            <div className="min-w-0">
              <p className="av-label">{kind === 'self' ? 'Your self score' : 'Evaluation score'}</p>
              <p className="font-bold">{live != null ? band(live) : 'Rate the areas above'}</p>
              <p className="text-xs text-slate-500 mt-0.5">
                {form.data?.weighted ? 'Weighted by area %' : 'Average of rated areas'} · N/A areas excluded
              </p>
            </div>
          </div>

          <div>
            <p className="av-label mb-1.5">{kind === 'self' ? 'Anything else for your manager' : 'Overall comments'}</p>
            <Textarea value={comments} disabled={readOnly} onChange={(e) => setComments(e.target.value)}
              placeholder={kind === 'self' ? 'Summarise your cycle…' : 'Summary for HR and the employee…'} />
          </div>

          <details className="rounded-xl border border-[var(--line)] p-3">
            <summary className="cursor-pointer text-xs font-bold text-slate-500 inline-flex items-center gap-1.5">
              <Info className="h-3.5 w-3.5" />What the ratings mean
            </summary>
            <RatingLegend className="mt-3" />
          </details>
        </div>
      )}
    </Modal>
  )
}

function ScoreDial({ score }: { score: number | null }) {
  const pct = score ?? 0
  return (
    <div className="grid place-items-center h-[86px] w-[86px] rounded-full shrink-0"
      style={{ background: `conic-gradient(var(--brand-ring, #1E7FD4) ${pct}%, rgb(226 232 240 / 1) 0)` }}>
      <div className="grid place-items-center h-[66px] w-[66px] rounded-full bg-[var(--card)]">
        <span className="text-xl font-extrabold leading-none">{score ?? '—'}</span>
        <span className="text-[9px] uppercase tracking-wide text-slate-400">/ 100</span>
      </div>
    </div>
  )
}

function ReleaseView({ scores, loading, finalRating, setFinalRating }: {
  scores?: { selfScore?: number | null; managerScore?: number | null; blendedScore?: number | null; selfWeightPct: number; managerWeightPct: number }
  loading: boolean
  finalRating: number | null
  setFinalRating: (v: number) => void
}) {
  if (loading) return <div className="space-y-3">{[...Array(3)].map((_, i) => <Skeleton key={i} className="h-20" />)}</div>
  const s = scores?.selfScore ?? null
  const m = scores?.managerScore ?? null
  const b = scores?.blendedScore ?? null

  return (
    <div className="space-y-4">
      <div className="grid sm:grid-cols-2 gap-3">
        <div className="rounded-xl border border-[var(--line)] p-4">
          <p className="av-label inline-flex items-center gap-1.5"><UserRound className="h-3.5 w-3.5" />Self · counts {scores?.selfWeightPct}%</p>
          <p className="text-2xl font-extrabold mt-1">{s ?? '—'}<span className="text-xs text-slate-400 font-semibold"> /100</span></p>
          <p className="text-xs text-slate-500">{s != null ? band(s) : 'Not submitted'}</p>
        </div>
        <div className="rounded-xl border border-[var(--line)] p-4">
          <p className="av-label inline-flex items-center gap-1.5"><ShieldCheck className="h-3.5 w-3.5" />Manager · counts {scores?.managerWeightPct}%</p>
          <p className="text-2xl font-extrabold mt-1">{m ?? '—'}<span className="text-xs text-slate-400 font-semibold"> /100</span></p>
          <p className="text-xs text-slate-500">{m != null ? band(m) : 'Not submitted'}</p>
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-4 rounded-xl border border-brand-100 bg-brand-50/60 p-4 dark:border-brand-900 dark:bg-brand-900/20">
        <ScoreDial score={b} />
        <div>
          <p className="av-label inline-flex items-center gap-1.5"><Scale className="h-3.5 w-3.5" />Blended final score</p>
          <p className="font-bold text-lg">{b != null ? band(b) : 'Awaiting both stages'}</p>
          {s != null && m != null && (
            <p className="text-xs text-slate-500 mt-0.5">({s} × {scores?.selfWeightPct}%) + ({m} × {scores?.managerWeightPct}%) = <b>{b}</b></p>
          )}
        </div>
        {b != null && <Badge t={bandTone(b)} className="ml-auto">{(b / 20).toFixed(1)} / 5 suggested</Badge>}
      </div>

      <div>
        <p className="av-label mb-2">Final rating — HR may override</p>
        <Rating1to5 value={finalRating != null ? Math.round(finalRating) : null} onChange={(v) => setFinalRating(v)} />
        <p className="text-xs text-slate-500 mt-2">Releasing notifies the employee and publishes the rating to their dashboard.</p>
      </div>
    </div>
  )
}
