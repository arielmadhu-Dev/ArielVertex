import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { GraduationCap, Plus, Sparkles, Play, Check } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import { useEmployees } from '../lib/hooks'
import type { Training } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, Badge, StatusPill, Avatar, Button, EmptyState } from '../ui/primitives'
import { Modal } from '../ui/Modal'
import { Field, Input, Select } from '../ui/form'
import { useToast } from '../ui/Toast'
import { P } from '../components/nav'

export default function Learning() {
  const { user, has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const employees = useEmployees()
  const canManage = has(P.LearningManage)
  const [open, setOpen] = useState(false)
  const [recFor, setRecFor] = useState<number | 0>(0)

  const { data } = useQuery({ queryKey: ['learning'], queryFn: async () => (await api.get<Training[]>('/learning')).data })
  const invalidate = () => qc.invalidateQueries({ queryKey: ['learning'] })

  const setStatus = useMutation({
    mutationFn: ({ id, status }: { id: number; status: string }) => api.put(`/learning/${id}/status`, { status }),
    onSuccess: () => { push('Training updated'); invalidate() },
    onError: (e) => push(apiError(e), 'error'),
  })
  const recommend = useMutation({
    mutationFn: (employeeId: number) => api.post(`/learning/recommend/${employeeId}`, {}),
    onSuccess: (r: any) => { push(r.data.length ? `${r.data.length} training path(s) suggested` : 'No new gaps found'); setRecFor(0); invalidate() },
    onError: (e) => push(apiError(e), 'error'),
  })

  return (
    <div>
      <PageHeader title="Learning & Development" subtitle="Assign training or auto-recommend from performance gaps" icon={<GraduationCap className="h-5 w-5" />}
        actions={canManage ? (
          <div className="flex items-center gap-2">
            <Select value={recFor} onChange={(e) => { const id = Number(e.target.value); if (id) recommend.mutate(id); }} className="w-52">
              <option value={0}>Auto-recommend for…</option>
              {employees.data?.map((e) => <option key={e.id} value={e.id}>{e.name}</option>)}
            </Select>
            <Button icon={<Plus className="h-4 w-4" />} onClick={() => setOpen(true)}>Assign</Button>
          </div>
        ) : undefined} />

      {data?.length ? (
        <div className="grid gap-3 md:grid-cols-2">
          {data.map((t) => {
            const isOwner = t.employeeId === user?.id
            return (
              <Card key={t.id} className="p-4 flex items-start gap-3">
                <Avatar name={t.employeeName} color={t.avatarColor} size={38} />
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2 flex-wrap">
                    <p className="font-bold truncate">{t.recommendedTraining}</p>
                    <StatusPill value={t.status} />
                    {t.source === 'AI' && <Badge t="brand"><Sparkles className="h-3 w-3" />AI</Badge>}
                  </div>
                  <p className="text-xs text-slate-500">{t.employeeName} · {t.skillGap} · {t.durationMonths} mo</p>
                  {(isOwner || canManage) && t.status !== 'Completed' && (
                    <div className="flex items-center gap-2 mt-2">
                      {t.status === 'Recommended' && <Button size="sm" variant="secondary" icon={<Play className="h-4 w-4" />} onClick={() => setStatus.mutate({ id: t.id, status: 'InProgress' })}>Start</Button>}
                      <Button size="sm" icon={<Check className="h-4 w-4" />} onClick={() => setStatus.mutate({ id: t.id, status: 'Completed' })}>Complete</Button>
                    </div>
                  )}
                </div>
              </Card>
            )
          })}
        </div>
      ) : <Card><EmptyState icon={<GraduationCap className="h-6 w-6" />} title="No training yet" hint={canManage ? 'Assign training or auto-recommend from goals.' : 'Assigned learning will appear here.'} /></Card>}

      {open && <AssignModal onClose={() => setOpen(false)} onDone={() => { invalidate(); setOpen(false) }} employees={employees.data ?? []} push={push} />}
    </div>
  )
}

function AssignModal({ onClose, onDone, employees, push }: any) {
  const [form, setForm] = useState({ employeeId: 0, skillGap: '', recommendedTraining: '', durationMonths: 3 })
  const create = useMutation({
    mutationFn: () => api.post('/learning', form),
    onSuccess: () => { push('Training assigned'); onDone() },
    onError: (e: any) => push(apiError(e), 'error'),
  })
  return (
    <Modal open onClose={onClose} title="Assign training" subtitle="Target a specific skill gap"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={create.isPending} disabled={!form.employeeId || !form.skillGap || !form.recommendedTraining} onClick={() => create.mutate()}>Assign</Button></>}>
      <div className="grid sm:grid-cols-2 gap-4">
        <Field label="Employee" required><Select value={form.employeeId} onChange={(e) => setForm({ ...form, employeeId: Number(e.target.value) })}><option value={0}>Select…</option>{employees.map((e: any) => <option key={e.id} value={e.id}>{e.name}</option>)}</Select></Field>
        <Field label="Duration (months)"><Input type="number" min={1} max={24} value={form.durationMonths} onChange={(e) => setForm({ ...form, durationMonths: Number(e.target.value) })} /></Field>
        <div className="sm:col-span-2"><Field label="Skill gap" required><Input value={form.skillGap} onChange={(e) => setForm({ ...form, skillGap: e.target.value })} placeholder="e.g. System design" /></Field></div>
        <div className="sm:col-span-2"><Field label="Recommended training" required><Input value={form.recommendedTraining} onChange={(e) => setForm({ ...form, recommendedTraining: e.target.value })} placeholder="e.g. Distributed systems course" /></Field></div>
      </div>
    </Modal>
  )
}
