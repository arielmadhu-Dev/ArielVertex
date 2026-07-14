import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { TrendingUp, Plus, Check, X } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import { useEmployees } from '../lib/hooks'
import type { Promotion } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, Badge, StatusPill, Avatar, Button, EmptyState } from '../ui/primitives'
import { Modal } from '../ui/Modal'
import { Field, Input, Select, Textarea } from '../ui/form'
import { useToast } from '../ui/Toast'
import { P } from '../components/nav'

const STAGES = ['ManagerRecommended', 'HrValidated', 'LeadershipApproved', 'Completed']
const nice = (s: string) => s.replace(/([a-z])([A-Z])/g, '$1 $2')

export default function Promotions() {
  const { has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const employees = useEmployees()
  const canRecommend = has(P.PromotionsRecommend) || has(P.PromotionsManage)
  const canValidate = has(P.PromotionsManage)
  const canApprove = has(P.PromotionsApprove)
  const [open, setOpen] = useState(false)

  const { data } = useQuery({ queryKey: ['promotions'], queryFn: async () => (await api.get<Promotion[]>('/promotions')).data })
  const invalidate = () => qc.invalidateQueries({ queryKey: ['promotions'] })

  const act = useMutation({
    mutationFn: ({ id, action }: { id: number; action: string }) => api.post(`/promotions/${id}/${action}`, {}),
    onSuccess: () => { push('Promotion updated'); invalidate() },
    onError: (e) => push(apiError(e), 'error'),
  })

  return (
    <div>
      <PageHeader title="Promotions & Increments" subtitle="Manager recommendation → HR validation → leadership approval → completion" icon={<TrendingUp className="h-5 w-5" />}
        actions={canRecommend ? <Button icon={<Plus className="h-4 w-4" />} onClick={() => setOpen(true)}>Recommend</Button> : undefined} />

      {data?.length ? (
        <div className="space-y-3">
          {data.map((p) => {
            const stepIdx = STAGES.indexOf(p.stage)
            const closed = p.stage === 'Completed' || p.stage === 'Rejected'
            return (
              <Card key={p.id} className="p-4">
                <div className="flex flex-wrap items-center gap-4">
                  <Avatar name={p.employeeName} color={p.avatarColor} size={42} />
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2 flex-wrap">
                      <p className="font-bold">{p.employeeName}</p>
                      <StatusPill value={p.stage} />
                    </div>
                    <p className="text-xs text-slate-500 mt-0.5">{p.currentDesignation} → <span className="font-semibold text-slate-700 dark:text-slate-200">{p.proposedDesignation}</span>{p.recommendedByName ? ` · by ${p.recommendedByName}` : ''}</p>
                    {p.justification && <p className="text-sm mt-1 text-slate-600 dark:text-slate-300">{p.justification}</p>}
                    {p.decisionNote && <p className="text-xs mt-1 italic text-slate-500">Note: {p.decisionNote}</p>}
                  </div>
                  <div className="flex items-center gap-2">
                    {p.stage === 'ManagerRecommended' && canValidate && <Button size="sm" onClick={() => act.mutate({ id: p.id, action: 'validate' })}>Validate</Button>}
                    {p.stage === 'HrValidated' && canApprove && <Button size="sm" onClick={() => act.mutate({ id: p.id, action: 'approve' })}>Approve</Button>}
                    {p.stage === 'LeadershipApproved' && canValidate && <Button size="sm" icon={<Check className="h-4 w-4" />} onClick={() => act.mutate({ id: p.id, action: 'complete' })}>Complete</Button>}
                    {!closed && (canValidate || canApprove) && <Button size="sm" variant="subtle" icon={<X className="h-4 w-4" />} onClick={() => act.mutate({ id: p.id, action: 'reject' })}>Reject</Button>}
                  </div>
                </div>
                {!closed && (
                  <div className="mt-3 flex items-center gap-1.5">
                    {STAGES.map((s, i) => (
                      <div key={s} className="flex-1 flex items-center gap-1.5">
                        <span className={`h-1.5 flex-1 rounded-full ${i <= stepIdx ? 'bg-brand-500' : 'bg-slate-200 dark:bg-navy-600'}`} />
                      </div>
                    ))}
                    <span className="text-[10px] text-slate-400 ml-1">{nice(p.stage)}</span>
                  </div>
                )}
              </Card>
            )
          })}
        </div>
      ) : <Card><EmptyState icon={<TrendingUp className="h-6 w-6" />} title="No promotions" hint={canRecommend ? 'Recommend an employee for promotion.' : 'Promotion cases will appear here.'} /></Card>}

      {open && <RecommendModal onClose={() => setOpen(false)} onDone={() => { invalidate(); setOpen(false) }} employees={employees.data ?? []} push={push} />}
    </div>
  )
}

function RecommendModal({ onClose, onDone, employees, push }: any) {
  const [form, setForm] = useState({ employeeId: 0, proposedDesignation: '', proposedSalary: '', justification: '' })
  const create = useMutation({
    mutationFn: () => api.post('/promotions', {
      employeeId: form.employeeId, proposedDesignation: form.proposedDesignation,
      proposedSalary: form.proposedSalary ? Number(form.proposedSalary) : null, justification: form.justification,
    }),
    onSuccess: () => { push('Promotion recommended'); onDone() },
    onError: (e: any) => push(apiError(e), 'error'),
  })
  return (
    <Modal open onClose={onClose} title="Recommend a promotion" subtitle="Raises a case into the approval workflow"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={create.isPending} disabled={!form.employeeId || !form.proposedDesignation} onClick={() => create.mutate()}>Recommend</Button></>}>
      <div className="grid sm:grid-cols-2 gap-4">
        <Field label="Employee" required><Select value={form.employeeId} onChange={(e) => setForm({ ...form, employeeId: Number(e.target.value) })}><option value={0}>Select…</option>{employees.map((e: any) => <option key={e.id} value={e.id}>{e.name}</option>)}</Select></Field>
        <Field label="Proposed designation" required><Input value={form.proposedDesignation} onChange={(e) => setForm({ ...form, proposedDesignation: e.target.value })} placeholder="Senior Software Engineer" /></Field>
        <Field label="Proposed salary (optional)"><Input type="number" value={form.proposedSalary} onChange={(e) => setForm({ ...form, proposedSalary: e.target.value })} /></Field>
        <div className="sm:col-span-2"><Field label="Justification"><Textarea value={form.justification} onChange={(e) => setForm({ ...form, justification: e.target.value })} /></Field></div>
      </div>
    </Modal>
  )
}
