import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { CalendarRange, Plus, Play, Lock } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import type { Cycle } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, Badge, StatusPill, Button, EmptyState } from '../ui/primitives'
import { Modal } from '../ui/Modal'
import { Field, Input } from '../ui/form'
import { useToast } from '../ui/Toast'
import { fmtDate } from '../ui/util'
import { P } from '../components/nav'

export default function Cycles() {
  const { has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const canManage = has(P.CyclesManage)
  const [open, setOpen] = useState(false)
  const [form, setForm] = useState({ name: '', startDate: '', endDate: '' })

  const { data } = useQuery({ queryKey: ['cycles'], queryFn: async () => (await api.get<Cycle[]>('/cycles')).data })
  const invalidate = () => qc.invalidateQueries({ queryKey: ['cycles'] })

  const create = useMutation({
    mutationFn: () => api.post('/cycles', form),
    onSuccess: () => { push('Cycle created'); setForm({ name: '', startDate: '', endDate: '' }); setOpen(false); invalidate() },
    onError: (e) => push(apiError(e), 'error'),
  })
  const act = useMutation({
    mutationFn: ({ id, action }: { id: number; action: 'activate' | 'close' }) => api.post(`/cycles/${id}/${action}`),
    onSuccess: (_r, v) => { push(v.action === 'activate' ? 'Cycle activated — appraisals generated' : 'Cycle closed'); invalidate() },
    onError: (e) => push(apiError(e), 'error'),
  })

  return (
    <div>
      <PageHeader title="Appraisal Cycles" subtitle="Create a review cycle, then activate it to open self-assessments for everyone" icon={<CalendarRange className="h-5 w-5" />}
        actions={canManage ? <Button icon={<Plus className="h-4 w-4" />} onClick={() => setOpen(true)}>New cycle</Button> : undefined} />

      {data?.length ? (
        <div className="space-y-3">
          {data.map((c) => (
            <Card key={c.id} className="p-4 flex flex-wrap items-center gap-4">
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2 flex-wrap">
                  <p className="font-bold">{c.name}</p>
                  <StatusPill value={c.status} />
                  <Badge t="info">{c.appraisalCount} appraisals</Badge>
                </div>
                <p className="text-xs text-slate-500 mt-0.5">{fmtDate(c.startDate)} → {fmtDate(c.endDate)}</p>
              </div>
              {canManage && (
                <div className="flex items-center gap-2">
                  {c.status === 'Draft' && <Button size="sm" icon={<Play className="h-4 w-4" />} loading={act.isPending} onClick={() => act.mutate({ id: c.id, action: 'activate' })}>Activate</Button>}
                  {c.status === 'Active' && <Button size="sm" variant="secondary" icon={<Lock className="h-4 w-4" />} loading={act.isPending} onClick={() => act.mutate({ id: c.id, action: 'close' })}>Close</Button>}
                </div>
              )}
            </Card>
          ))}
        </div>
      ) : <Card><EmptyState icon={<CalendarRange className="h-6 w-6" />} title="No appraisal cycles yet" hint={canManage ? 'Create a cycle to kick off appraisals.' : 'HR will open a cycle when appraisals are due.'} /></Card>}

      <Modal open={open} onClose={() => setOpen(false)} title="New appraisal cycle" subtitle="e.g. Q3 2026 Appraisal"
        footer={<><Button variant="secondary" onClick={() => setOpen(false)}>Cancel</Button><Button loading={create.isPending} disabled={!form.name || !form.startDate || !form.endDate} onClick={() => create.mutate()}>Create</Button></>}>
        <div className="grid sm:grid-cols-2 gap-4">
          <div className="sm:col-span-2"><Field label="Cycle name" required><Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="Q3 2026 Appraisal" /></Field></div>
          <Field label="Start date" required><Input type="date" value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} /></Field>
          <Field label="End date" required><Input type="date" value={form.endDate} onChange={(e) => setForm({ ...form, endDate: e.target.value })} /></Field>
        </div>
      </Modal>
    </div>
  )
}
