import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Target, Plus, Trash2 } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import { useEnums, useEmployees } from '../lib/hooks'
import type { Goal } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, Badge, StatusPill, Avatar, Button, EmptyState } from '../ui/primitives'
import { Modal } from '../ui/Modal'
import { Field, Input, Select, Textarea } from '../ui/form'
import { useToast } from '../ui/Toast'
import { fmtDate } from '../ui/util'
import { P } from '../components/nav'

export default function Goals() {
  const { user, has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const enums = useEnums()
  const employees = useEmployees()
  const canAssign = has(P.GoalsAssign)
  const [open, setOpen] = useState(false)

  const { data } = useQuery({ queryKey: ['goals'], queryFn: async () => (await api.get<Goal[]>('/goals')).data })
  const invalidate = () => qc.invalidateQueries({ queryKey: ['goals'] })

  const update = useMutation({
    mutationFn: ({ id, progress, status }: { id: number; progress: number; status: string }) => api.put(`/goals/${id}`, { progress, status }),
    onSuccess: () => { push('Goal updated'); invalidate() },
    onError: (e) => push(apiError(e), 'error'),
  })
  const remove = useMutation({
    mutationFn: (id: number) => api.delete(`/goals/${id}`),
    onSuccess: () => { push('Goal removed'); invalidate() },
    onError: (e) => push(apiError(e), 'error'),
  })

  return (
    <div>
      <PageHeader title="Goals & KRAs" subtitle="Assign weighted goals and track progress toward the target date" icon={<Target className="h-5 w-5" />}
        actions={canAssign ? <Button icon={<Plus className="h-4 w-4" />} onClick={() => setOpen(true)}>Assign goal</Button> : undefined} />

      {data?.length ? (
        <div className="grid gap-3 md:grid-cols-2">
          {data.map((g) => {
            const isOwner = g.employeeId === user?.id
            return (
              <Card key={g.id} className="p-4">
                <div className="flex items-start gap-3">
                  <Avatar name={g.employeeName} color={g.avatarColor} size={38} />
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2 flex-wrap">
                      <p className="font-bold truncate">{g.title}</p>
                      <StatusPill value={g.status} />
                    </div>
                    <p className="text-xs text-slate-500">{g.employeeName}{g.category ? ` · ${g.category}` : ''} · {g.weightage}% · due {fmtDate(g.targetDate)}</p>
                    {g.description && <p className="text-sm mt-1 text-slate-600 dark:text-slate-300">{g.description}</p>}
                    <div className="mt-3">
                      <div className="flex items-center justify-between text-xs mb-1"><span className="text-slate-500">Progress</span><span className="font-semibold">{g.progress}%</span></div>
                      <div className="h-2 rounded-full bg-slate-100 dark:bg-navy-600 overflow-hidden"><div className="h-full bg-brand-500 rounded-full" style={{ width: `${g.progress}%` }} /></div>
                      {(isOwner || canAssign) && g.status !== 'Completed' && g.status !== 'Cancelled' && (
                        <div className="flex items-center gap-2 mt-2">
                          <input type="range" min={0} max={100} step={5} defaultValue={g.progress}
                            onMouseUp={(e) => update.mutate({ id: g.id, progress: Number((e.target as HTMLInputElement).value), status: 'InProgress' })}
                            onTouchEnd={(e) => update.mutate({ id: g.id, progress: Number((e.target as HTMLInputElement).value), status: 'InProgress' })}
                            className="flex-1 accent-brand-600" />
                          <Button size="sm" variant="secondary" onClick={() => update.mutate({ id: g.id, progress: 100, status: 'Completed' })}>Complete</Button>
                        </div>
                      )}
                    </div>
                  </div>
                  {canAssign && <button onClick={() => remove.mutate(g.id)} className="text-slate-400 hover:text-rose-500 p-1"><Trash2 className="h-4 w-4" /></button>}
                </div>
              </Card>
            )
          })}
        </div>
      ) : <Card><EmptyState icon={<Target className="h-6 w-6" />} title="No goals yet" hint={canAssign ? 'Assign the first goal to an employee.' : 'Goals assigned to you will appear here.'} /></Card>}

      {open && <AssignGoalModal onClose={() => setOpen(false)} onDone={() => { invalidate(); setOpen(false) }}
        employees={employees.data ?? []} categories={enums.data?.goalCategories ?? []} push={push} />}
    </div>
  )
}

function AssignGoalModal({ onClose, onDone, employees, categories, push }: any) {
  const [form, setForm] = useState({ employeeId: 0, title: '', description: '', category: '', weightage: 20, targetDate: '' })
  const create = useMutation({
    mutationFn: () => api.post('/goals', { ...form, category: form.category || (categories[0]?.value ?? '') }),
    onSuccess: () => { push('Goal assigned'); onDone() },
    onError: (e: any) => push(apiError(e), 'error'),
  })
  return (
    <Modal open onClose={onClose} title="Assign a goal" subtitle="Set a KRA category, weightage and target date"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={create.isPending} disabled={!form.employeeId || !form.title || !form.targetDate} onClick={() => create.mutate()}>Assign</Button></>}>
      <div className="grid sm:grid-cols-2 gap-4">
        <Field label="Employee" required><Select value={form.employeeId} onChange={(e) => setForm({ ...form, employeeId: Number(e.target.value) })}><option value={0}>Select…</option>{employees.map((e: any) => <option key={e.id} value={e.id}>{e.name}</option>)}</Select></Field>
        <Field label="KRA category"><Select value={form.category} onChange={(e) => setForm({ ...form, category: e.target.value })}>{categories.map((c: any) => <option key={c.value} value={c.value}>{c.label}</option>)}</Select></Field>
        <div className="sm:col-span-2"><Field label="Goal title" required><Input value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} placeholder="e.g. Ship the billing API v2" /></Field></div>
        <Field label="Weightage (%)"><Input type="number" min={0} max={100} value={form.weightage} onChange={(e) => setForm({ ...form, weightage: Number(e.target.value) })} /></Field>
        <Field label="Target date" required><Input type="date" value={form.targetDate} onChange={(e) => setForm({ ...form, targetDate: e.target.value })} /></Field>
        <div className="sm:col-span-2"><Field label="Description"><Textarea value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></Field></div>
      </div>
    </Modal>
  )
}
