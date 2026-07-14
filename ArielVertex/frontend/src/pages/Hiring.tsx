import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { UserPlus, Plus } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import { useEnums } from '../lib/hooks'
import type { ResourceRequestItem } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, StatusPill, Badge, Button, EmptyState } from '../ui/primitives'
import { Modal } from '../ui/Modal'
import { Field, Input, Select, Textarea } from '../ui/form'
import { useToast } from '../ui/Toast'
import { fmtDate } from '../ui/util'
import { P } from '../components/nav'

export default function Hiring() {
  const { has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const enums = useEnums()
  const [open, setOpen] = useState(false)
  const canManage = has(P.ResourcesManage)

  const { data } = useQuery({ queryKey: ['hiring'], queryFn: async () => (await api.get<ResourceRequestItem[]>('/resource-requests')).data })
  const projects = useQuery({ queryKey: ['projects', ''], queryFn: async () => (await api.get('/projects', { params: { pageSize: 50 } })).data.items as any[] })
  const invalidate = () => qc.invalidateQueries({ queryKey: ['hiring'] })

  const [form, setForm] = useState({ projectId: 0, roleTitle: '', skills: '', reason: '', priority: 'Medium', count: 1, expectedStartDate: '' })
  const create = useMutation({
    mutationFn: () => api.post('/resource-requests', { ...form, expectedStartDate: form.expectedStartDate || null }),
    onSuccess: () => { push('Resource request raised'); setOpen(false); invalidate() },
    onError: (e) => push(apiError(e), 'error'),
  })
  const setStatus = useMutation({
    mutationFn: ({ id, status }: { id: number; status: string }) => api.post(`/resource-requests/${id}/status`, { status }),
    onSuccess: () => { push('Status updated'); invalidate() },
    onError: (e) => push(apiError(e), 'error'),
  })

  return (
    <div>
      <PageHeader title="Hiring Requests" subtitle="Resource requests from projects, tracked by HR" icon={<UserPlus className="h-5 w-5" />}
        actions={has(P.ResourcesRequest) ? <Button icon={<Plus className="h-4 w-4" />} onClick={() => setOpen(true)}>Raise request</Button> : undefined} />

      {data?.length ? (
        <div className="space-y-3">
          {data.map((r) => (
            <Card key={r.id} className="p-5">
              <div className="flex flex-wrap items-start gap-4">
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2 flex-wrap">
                    <p className="font-bold">{r.roleTitle}</p>
                    <Badge t="info">×{r.count}</Badge>
                    <Badge t={r.priority === 'Critical' || r.priority === 'High' ? 'danger' : 'neutral'}>{r.priority}</Badge>
                    <StatusPill value={r.status} />
                  </div>
                  <p className="text-xs text-slate-500 mt-0.5">{r.projectName} · by {r.requestedByName} · {fmtDate(r.createdAt)}</p>
                  {r.skills && <p className="text-sm mt-1"><span className="font-semibold">Skills:</span> {r.skills}</p>}
                  {r.reason && <p className="text-sm"><span className="font-semibold">Reason:</span> {r.reason}</p>}
                </div>
                {canManage && (
                  <Select value={r.status} onChange={(e) => setStatus.mutate({ id: r.id, status: e.target.value })} className="w-48">
                    {enums.data?.resourceStatuses.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                  </Select>
                )}
              </div>
            </Card>
          ))}
        </div>
      ) : <Card><EmptyState icon={<UserPlus className="h-6 w-6" />} title="No hiring requests" hint="Raise a resource request from a project you manage." /></Card>}

      <Modal open={open} onClose={() => setOpen(false)} title="Raise resource request"
        footer={<><Button variant="secondary" onClick={() => setOpen(false)}>Cancel</Button><Button loading={create.isPending} disabled={!form.projectId || !form.roleTitle} onClick={() => create.mutate()}>Submit request</Button></>}>
        <div className="grid sm:grid-cols-2 gap-4">
          <Field label="Project" required><Select value={form.projectId} onChange={(e) => setForm({ ...form, projectId: Number(e.target.value) })}><option value={0}>Select…</option>{projects.data?.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</Select></Field>
          <Field label="Role title" required><Input value={form.roleTitle} onChange={(e) => setForm({ ...form, roleTitle: e.target.value })} placeholder="Senior React Developer" /></Field>
          <Field label="Count"><Input type="number" min={1} value={form.count} onChange={(e) => setForm({ ...form, count: Number(e.target.value) })} /></Field>
          <Field label="Priority"><Select value={form.priority} onChange={(e) => setForm({ ...form, priority: e.target.value })}>{enums.data?.priorities.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select></Field>
          <div className="sm:col-span-2"><Field label="Skills"><Input value={form.skills} onChange={(e) => setForm({ ...form, skills: e.target.value })} placeholder="React, TypeScript…" /></Field></div>
          <Field label="Expected start"><Input type="date" value={form.expectedStartDate} onChange={(e) => setForm({ ...form, expectedStartDate: e.target.value })} /></Field>
          <div className="sm:col-span-2"><Field label="Reason"><Textarea value={form.reason} onChange={(e) => setForm({ ...form, reason: e.target.value })} /></Field></div>
        </div>
      </Modal>
    </div>
  )
}
