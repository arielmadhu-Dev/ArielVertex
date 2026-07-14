import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { LifeBuoy, Plus, Sparkles, Pencil } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import { useEmployees } from '../lib/hooks'
import { PageHeader } from '../components/PageHeader'
import { Card, StatusPill, Badge, Avatar, Button, EmptyState, Skeleton } from '../ui/primitives'
import { Modal } from '../ui/Modal'
import { Field, Input, Select, Textarea } from '../ui/form'
import { useToast } from '../ui/Toast'
import { fmtDate } from '../ui/util'
import { P } from '../components/nav'

interface Pip {
  id: number; subjectUserId: number; subjectName: string; avatarColor: string; reason: string
  expectedImprovement: string; supportProvided: string; status: string; statusLabel: string
  outcome: string; outcomeLabel: string; reviewNotes?: string; startDate: string
  isAuto: boolean; triggerScore?: number; triggerPeriod?: string; createdByName?: string; createdAt: string
}

export default function PipPage() {
  const { has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const canView = has(P.PipView)
  const canManage = has(P.PipManage)
  const [createOpen, setCreateOpen] = useState(false)
  const [edit, setEdit] = useState<Pip | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['pip', canView],
    queryFn: async () => (await api.get<Pip[]>(canView ? '/pip' : '/pip/mine')).data,
  })
  const invalidate = () => qc.invalidateQueries({ queryKey: ['pip'] })

  return (
    <div>
      <PageHeader title={canView ? 'Performance Improvement Plans' : 'My Improvement Plan'}
        subtitle={canView ? 'Auto-initiated when an approved score falls below the configured threshold' : 'Your active plan and progress'}
        icon={<LifeBuoy className="h-5 w-5" />}
        actions={canManage ? <Button icon={<Plus className="h-4 w-4" />} onClick={() => setCreateOpen(true)}>New PIP</Button> : undefined} />

      {isLoading ? <div className="space-y-3">{[...Array(3)].map((_, i) => <Skeleton key={i} className="h-28" />)}</div>
        : data?.length ? (
          <div className="space-y-3">
            {data.map((p) => (
              <Card key={p.id} className="p-5">
                <div className="flex flex-wrap items-start gap-4">
                  <Avatar name={p.subjectName} color={p.avatarColor} size={46} />
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2 flex-wrap">
                      <p className="font-bold">{p.subjectName}</p>
                      <StatusPill value={p.status} />
                      <Badge t={p.outcome === 'Improved' ? 'good' : p.outcome === 'NotImproved' ? 'danger' : 'neutral'}>{p.outcomeLabel}</Badge>
                      {p.isAuto && <Badge t="warn"><Sparkles className="h-3.5 w-3.5" />Auto</Badge>}
                    </div>
                    <p className="text-sm mt-1">{p.reason}</p>
                    {p.expectedImprovement && <p className="text-sm mt-1"><span className="font-semibold">Expected:</span> {p.expectedImprovement}</p>}
                    {p.supportProvided && <p className="text-sm"><span className="font-semibold">Support:</span> {p.supportProvided}</p>}
                    {p.reviewNotes && <p className="text-sm mt-1 text-slate-500"><span className="font-semibold">Review:</span> {p.reviewNotes}</p>}
                    <p className="text-xs text-slate-400 mt-1.5">Started {fmtDate(p.startDate)}{p.triggerScore != null ? ` · triggered at ${p.triggerScore}% (${p.triggerPeriod})` : ''}{p.createdByName ? ` · by ${p.createdByName}` : ''}</p>
                  </div>
                  {canManage && <Button size="sm" variant="secondary" icon={<Pencil className="h-4 w-4" />} onClick={() => setEdit(p)}>Update</Button>}
                </div>
              </Card>
            ))}
          </div>
        ) : <Card><EmptyState icon={<LifeBuoy className="h-6 w-6" />} title={canView ? 'No improvement plans' : "You're not on a plan"} hint={canView ? 'Plans auto-start when a published score is below the threshold.' : 'Nothing to show here.'} /></Card>}

      {createOpen && <CreatePipModal onClose={() => setCreateOpen(false)} onDone={() => { invalidate(); setCreateOpen(false) }} push={push} />}
      {edit && <UpdatePipModal pip={edit} onClose={() => setEdit(null)} onDone={() => { invalidate(); setEdit(null) }} push={push} />}
    </div>
  )
}

function CreatePipModal({ onClose, onDone, push }: any) {
  const employees = useEmployees()
  const [form, setForm] = useState({ subjectUserId: 0, reason: '', expectedImprovement: '', supportProvided: '' })
  const create = useMutation({
    mutationFn: () => api.post('/pip', form),
    onSuccess: () => { push('PIP created'); onDone() },
    onError: (e: any) => push(apiError(e), 'error'),
  })
  return (
    <Modal open onClose={onClose} title="New Performance Improvement Plan"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={create.isPending} disabled={!form.subjectUserId || !form.reason} onClick={() => create.mutate()}>Create</Button></>}>
      <div className="space-y-4">
        <Field label="Employee" required><Select value={form.subjectUserId} onChange={(e) => setForm({ ...form, subjectUserId: Number(e.target.value) })}><option value={0}>Select…</option>{employees.data?.map((e) => <option key={e.id} value={e.id}>{e.name}</option>)}</Select></Field>
        <Field label="Reason" required><Textarea value={form.reason} onChange={(e) => setForm({ ...form, reason: e.target.value })} /></Field>
        <Field label="Expected improvement"><Textarea value={form.expectedImprovement} onChange={(e) => setForm({ ...form, expectedImprovement: e.target.value })} /></Field>
        <Field label="Support provided"><Textarea value={form.supportProvided} onChange={(e) => setForm({ ...form, supportProvided: e.target.value })} /></Field>
      </div>
    </Modal>
  )
}

function UpdatePipModal({ pip, onClose, onDone, push }: any) {
  const [form, setForm] = useState({ status: pip.status, outcome: pip.outcome, reviewNotes: pip.reviewNotes ?? '' })
  const save = useMutation({
    mutationFn: () => api.put(`/pip/${pip.id}`, form),
    onSuccess: () => { push('PIP updated'); onDone() },
    onError: (e: any) => push(apiError(e), 'error'),
  })
  return (
    <Modal open onClose={onClose} title={`Update PIP — ${pip.subjectName}`}
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={save.isPending} onClick={() => save.mutate()}>Save</Button></>}>
      <div className="space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <Field label="Status"><Select value={form.status} onChange={(e) => setForm({ ...form, status: e.target.value })}>{['Open', 'InProgress', 'Completed', 'Closed'].map((s) => <option key={s} value={s}>{s.replace(/([a-z])([A-Z])/g, '$1 $2')}</option>)}</Select></Field>
          <Field label="Outcome"><Select value={form.outcome} onChange={(e) => setForm({ ...form, outcome: e.target.value })}>{['Pending', 'Improved', 'NotImproved'].map((s) => <option key={s} value={s}>{s.replace(/([a-z])([A-Z])/g, '$1 $2')}</option>)}</Select></Field>
        </div>
        <Field label="Review notes"><Textarea value={form.reviewNotes} onChange={(e) => setForm({ ...form, reviewNotes: e.target.value })} /></Field>
      </div>
    </Modal>
  )
}
