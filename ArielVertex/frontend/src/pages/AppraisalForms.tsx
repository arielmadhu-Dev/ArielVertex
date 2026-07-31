import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  ClipboardList, Plus, Trash2, Save, Star, AlignLeft, Sparkles, UserRound, ShieldCheck, Scale, Info,
} from 'lucide-react'
import { api, apiError } from '../lib/api'
import {
  useFormRoles, useAppraisalForm, type FormArea, type FormVariant, type AreaType,
} from '../lib/appraisalForms'
import { PageHeader } from '../components/PageHeader'
import { Card, CardHeader, Button, Badge, Skeleton } from '../ui/primitives'
import { RatingLegend } from '../ui/RatingScale'
import { Input } from '../ui/form'
import { useToast } from '../ui/Toast'
import { cx } from '../ui/util'

/** HR configuration for the role-based appraisal forms (Self + Manager) and the score split. */
export default function AppraisalForms() {
  const { push } = useToast()
  const qc = useQueryClient()
  const roles = useFormRoles()

  const [role, setRole] = useState<string>('')
  const [variant, setVariant] = useState<FormVariant>('Manager')

  useEffect(() => {
    if (!role && roles.data?.roles.length) setRole(roles.data.roles[0])
  }, [roles.data, role])

  const form = useAppraisalForm(role, variant)

  // Local draft so edits feel instant; reloaded whenever the fetched form changes.
  const [areas, setAreas] = useState<FormArea[]>([])
  const [weighted, setWeighted] = useState(false)
  useEffect(() => {
    if (form.data) { setAreas(form.data.areas.map((a) => ({ ...a }))); setWeighted(form.data.weighted) }
  }, [form.data])

  const [selfPct, setSelfPct] = useState<number>(30)
  useEffect(() => { if (roles.data) setSelfPct(roles.data.selfWeightPct) }, [roles.data])

  const isSelf = variant === 'Self'
  const rated = areas.filter((a) => a.type === 'Rating')
  const totalWeight = useMemo(() => rated.reduce((s, a) => s + (a.weight || 0), 0), [rated])
  const weightsValid = !weighted || isSelf || totalWeight === 100

  const patch = (i: number, values: Partial<FormArea>) =>
    setAreas((cur) => cur.map((a, idx) => (idx === i ? { ...a, ...values } : a)))

  const save = useMutation({
    mutationFn: () => api.put('/appraisal-forms', { role, variant, weighted, areas }),
    onSuccess: () => {
      push(`${role} — ${isSelf ? 'self-assessment' : 'manager'} form saved`)
      qc.invalidateQueries({ queryKey: ['appraisal-form', role, variant] })
      qc.invalidateQueries({ queryKey: ['appraisal-form-roles'] })
    },
    onError: (e) => push(apiError(e), 'error'),
  })

  const saveSplit = useMutation({
    mutationFn: () => api.put('/appraisal-forms/self-weight', { selfWeightPct: selfPct }),
    onSuccess: () => { push(`Split saved — employee ${selfPct}% / manager ${100 - selfPct}%`); qc.invalidateQueries({ queryKey: ['appraisal-form-roles'] }) },
    onError: (e) => push(apiError(e), 'error'),
  })

  return (
    <div>
      <PageHeader
        title="Appraisal Forms"
        subtitle="Configure what each role is appraised on — the employee's self-assessment and the reporting manager's evaluation."
        icon={<ClipboardList className="h-5 w-5" />}
        actions={
          <Button icon={<Save className="h-4 w-4" />} loading={save.isPending} disabled={!weightsValid || !areas.length}
            onClick={() => save.mutate()}>
            Save {isSelf ? 'self' : 'manager'} form
          </Button>
        } />

      {/* Role picker */}
      <div className="flex flex-wrap gap-2 mb-4">
        {roles.isLoading
          ? [...Array(4)].map((_, i) => <Skeleton key={i} className="h-[52px] w-44" />)
          : roles.data?.roles.map((r) => (
            <button key={r} onClick={() => setRole(r)}
              className={cx('rounded-xl border px-4 py-2.5 text-left transition',
                r === role
                  ? 'border-brand-400 bg-brand-50 dark:bg-brand-900/30 shadow-sm'
                  : 'border-[var(--line)] bg-[var(--card)] hover:border-brand-200')}>
              <span className={cx('block text-sm font-bold', r === role ? 'text-brand-700 dark:text-brand-200' : '')}>{r}</span>
              <span className="block text-[11px] text-slate-400">role template</span>
            </button>
          ))}
      </div>

      <div className="grid lg:grid-cols-3 gap-4 items-start">
        <div className="lg:col-span-2 space-y-4">
          {/* Variant switch */}
          <div className="flex flex-wrap items-center gap-2">
            <div className="inline-flex rounded-xl border border-[var(--line)] overflow-hidden">
              {(['Self', 'Manager'] as FormVariant[]).map((v) => (
                <button key={v} onClick={() => setVariant(v)}
                  className={cx('px-4 py-2 text-sm font-bold transition inline-flex items-center gap-2',
                    variant === v ? 'bg-brand-500 text-white' : 'bg-[var(--card)] text-slate-500 hover:text-slate-700')}>
                  {v === 'Self' ? <UserRound className="h-4 w-4" /> : <ShieldCheck className="h-4 w-4" />}
                  {v === 'Self' ? 'Self-assessment' : 'Manager evaluation'}
                </button>
              ))}
            </div>
            <span className="text-xs text-slate-500">
              {isSelf ? 'Filled by the employee at stage 1' : 'Filled by the reporting manager at stage 2'}
            </span>
          </div>

          <Card className="overflow-hidden">
            <CardHeader
              title={`${role || '—'} · ${isSelf ? 'Self-assessment' : 'Manager evaluation'}`}
              subtitle={isSelf
                ? 'Reflective — short self-ratings plus open questions. Not a mirror of the manager scorecard.'
                : 'The scorecard the reporting manager rates 1–5 with comments.'}
              icon={isSelf ? <UserRound className="h-[18px] w-[18px]" /> : <ShieldCheck className="h-[18px] w-[18px]" />}
              action={!isSelf ? (
                <button onClick={() => setWeighted((w) => !w)}
                  className="flex items-center gap-2 text-xs font-semibold text-slate-500 hover:text-slate-700">
                  <span className={cx('relative h-6 w-11 rounded-full transition', weighted ? 'bg-brand-500' : 'bg-slate-300 dark:bg-navy-500')}>
                    <span className={cx('absolute top-0.5 h-5 w-5 rounded-full bg-white transition-all', weighted ? 'left-[22px]' : 'left-0.5')} />
                  </span>
                  Weighted
                </button>
              ) : undefined} />

            {form.isLoading ? (
              <div className="p-5 space-y-2">{[...Array(6)].map((_, i) => <Skeleton key={i} className="h-11" />)}</div>
            ) : (
              <div className="p-4 sm:p-5">
                {weighted && !isSelf && (
                  <div className={cx('mb-4 flex items-center gap-2.5 rounded-xl border px-3.5 py-2.5 text-sm font-semibold',
                    totalWeight === 100
                      ? 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-900/30 dark:text-emerald-300'
                      : 'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900 dark:bg-amber-900/30 dark:text-amber-300')}>
                    <Scale className="h-4 w-4" />
                    Weights total {totalWeight}% {totalWeight === 100 ? '— balanced' : '— must equal 100% to save'}
                  </div>
                )}

                <div className="space-y-2">
                  {areas.map((a, i) => (
                    <div key={i} className="group flex flex-wrap items-center gap-2.5 rounded-xl border border-[var(--line)] p-2.5 hover:border-brand-200 transition">
                      <span className="grid place-items-center h-7 w-7 shrink-0 rounded-lg bg-slate-100 dark:bg-navy-600 text-[11px] font-bold text-slate-500">{i + 1}</span>

                      <Input value={a.name} onChange={(e) => patch(i, { name: e.target.value })}
                        className="flex-1 min-w-[180px] !py-1.5 font-semibold" placeholder="Area or question" />

                      {isSelf && (
                        <button onClick={() => patch(i, { type: a.type === 'Rating' ? 'Text' as AreaType : 'Rating' as AreaType })}
                          className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--line)] px-2.5 py-1.5 text-[11px] font-bold text-slate-500 hover:text-brand-600 hover:border-brand-200">
                          {a.type === 'Rating' ? <><Star className="h-3.5 w-3.5" />Rating</> : <><AlignLeft className="h-3.5 w-3.5" />Text</>}
                        </button>
                      )}

                      {!isSelf && weighted && a.type === 'Rating' && (
                        <div className="inline-flex items-center gap-1">
                          <Input type="number" min={0} max={100} value={a.weight}
                            onChange={(e) => patch(i, { weight: Number(e.target.value) })}
                            className="w-16 !py-1.5 text-right" />
                          <span className="text-xs font-semibold text-slate-400">%</span>
                        </div>
                      )}

                      {a.type === 'Rating' && (
                        <label className="inline-flex items-center gap-1.5 text-[11px] font-semibold text-slate-500 whitespace-nowrap">
                          <input type="checkbox" className="h-3.5 w-3.5" checked={a.allowNa}
                            onChange={(e) => patch(i, { allowNa: e.target.checked })} />
                          if applicable
                        </label>
                      )}

                      <button onClick={() => setAreas((cur) => cur.filter((_, idx) => idx !== i))}
                        className="grid place-items-center h-7 w-7 rounded-lg text-slate-300 hover:text-rose-500 hover:bg-rose-50 dark:hover:bg-rose-900/30">
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  ))}
                </div>

                {!isSelf && (
                  <div className="mt-3 flex items-center gap-2.5 rounded-xl border border-brand-100 bg-brand-50/60 px-3.5 py-2.5 dark:border-brand-900 dark:bg-brand-900/20">
                    <Sparkles className="h-4 w-4 text-brand-500" />
                    <span className="text-sm font-bold">Overall Performance</span>
                    <Badge t="brand">auto-calculated</Badge>
                  </div>
                )}

                <button
                  onClick={() => setAreas((cur) => [...cur, { name: '', type: isSelf ? 'Text' : 'Rating', weight: 0, allowNa: false }])}
                  className="mt-3 w-full rounded-xl border border-dashed border-[var(--line)] py-2.5 text-sm font-bold text-slate-500 hover:border-brand-400 hover:text-brand-600 hover:bg-brand-50/50 dark:hover:bg-brand-900/20 transition inline-flex items-center justify-center gap-2">
                  <Plus className="h-4 w-4" />Add {isSelf ? 'question' : 'evaluation area'}
                </button>
              </div>
            )}
          </Card>
        </div>

        {/* Side rail */}
        <div className="space-y-4">
          <Card>
            <CardHeader title="Final score weighting" subtitle="How the two stages combine" icon={<Scale className="h-[18px] w-[18px]" />} />
            <div className="p-5">
              <div className="flex items-center justify-between text-sm font-bold">
                <span className="text-brand-600 dark:text-brand-300">Employee {selfPct}%</span>
                <span className="text-slate-500">Manager {100 - selfPct}%</span>
              </div>
              <input type="range" min={0} max={100} step={5} value={selfPct}
                onChange={(e) => setSelfPct(Number(e.target.value))}
                className="mt-3 w-full accent-brand-500" />
              <p className="mt-2 text-xs text-slate-500">
                The employee's self-assessment counts {selfPct}% toward the final score; the manager's evaluation counts {100 - selfPct}%. HR can still override at release.
              </p>
              <Button size="sm" variant="secondary" className="mt-3 w-full justify-center"
                loading={saveSplit.isPending} onClick={() => saveSplit.mutate()}>Save split</Button>
            </div>
          </Card>

          <Card>
            <CardHeader title="Rating scale" subtitle="Shown to every rater" icon={<Info className="h-[18px] w-[18px]" />} />
            <div className="p-4"><RatingLegend /></div>
          </Card>
        </div>
      </div>
    </div>
  )
}
