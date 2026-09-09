import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { LifeBuoy, Plus, MessageSquareReply } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import type { HelpdeskTicket } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, Button, EmptyState, Skeleton, StatusPill } from '../ui/primitives'
import { Modal } from '../ui/Modal'
import { Field, Input, Textarea, Select } from '../ui/form'
import { useToast } from '../ui/Toast'
import { fmtDate, fmtDateTime } from '../ui/util'
import { P } from '../components/nav'

const STATUS_OPTS = [
  { value: 'Open', label: 'Open' },
  { value: 'InProgress', label: 'In Progress' },
  { value: 'Resolved', label: 'Resolved' },
  { value: 'Closed', label: 'Closed' },
]

export default function Helpdesk() {
  const { has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const canManage = has(P.HelpdeskManage)
  const canRaise = has(P.HelpdeskRaise)
  const [open, setOpen] = useState(false)
  const [detail, setDetail] = useState<HelpdeskTicket | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['helpdesk'],
    queryFn: async () => (await api.get<HelpdeskTicket[]>('/helpdesk')).data,
    enabled: canRaise || canManage
  })
  const invalidate = () => qc.invalidateQueries({ queryKey: ['helpdesk'] })

  const [form, setForm] = useState({ subject: '', description: '', category: 'General', priority: 'Medium' })
  const create = useMutation({
    mutationFn: () => api.post('/helpdesk', form),
    onSuccess: () => { push('Ticket raised'); setOpen(false); invalidate(); setForm({ subject: '', description: '', category: 'General', priority: 'Medium' }) },
    onError: (e) => push(apiError(e), 'error'),
  })

  const [updateForm, setUpdateForm] = useState({ status: '', resolution: '' })
  const update = useMutation({
    mutationFn: () => api.put(`/helpdesk/${detail!.id}`, updateForm),
    onSuccess: () => { push('Ticket updated'); setDetail(null); invalidate() },
    onError: (e) => push(apiError(e), 'error'),
  })

  const items = data ?? []

  return (
    <div>
      <PageHeader title="Helpdesk" subtitle="Raise and resolve support tickets" icon={<LifeBuoy className="h-5 w-5" />}
        actions={canRaise ? <Button icon={<Plus className="h-4 w-4" />} onClick={() => setOpen(true)}>Raise Ticket</Button> : undefined} />

      <Card className="overflow-hidden">
        <div className="overflow-x-auto av-scroll-x">
          <table className="w-full text-sm">
            <thead><tr className="text-left text-xs uppercase tracking-wide text-slate-400 border-b border-[var(--line)]">
              <th className="px-5 py-3">ID</th>
              <th className="px-5 py-3">Subject</th>
              <th className="px-5 py-3">Category</th>
              <th className="px-5 py-3">Priority</th>
              <th className="px-5 py-3">Status</th>
              <th className="px-5 py-3">Raised By</th>
              <th className="px-5 py-3">Assigned To</th>
              <th className="px-5 py-3">Created</th>
              <th className="px-5 py-3 text-right">Actions</th>
            </tr></thead>
            <tbody>
              {isLoading ? [...Array(4)].map((_, i) => <tr key={i}><td colSpan={9} className="px-5 py-2"><Skeleton className="h-9" /></td></tr>)
                : items.length ? items.map((item) => (
                  <tr key={item.id} className="border-b border-[var(--line)] last:border-0 hover:bg-slate-50 dark:hover:bg-navy-600">
                    <td className="px-5 py-3 text-slate-500">#{item.id}</td>
                    <td className="px-5 py-3">
                      <button onClick={() => setDetail(item)} className="font-medium text-left hover:text-brand-600">{item.subject}</button>
                      {item.resolution && <p className="text-xs text-slate-400 mt-0.5 line-clamp-1">{item.resolution}</p>}
                    </td>
                    <td className="px-5 py-3 text-xs">{item.category}</td>
                    <td className="px-5 py-3"><StatusPill value={item.priority} /></td>
                    <td className="px-5 py-3"><StatusPill value={item.status} /></td>
                    <td className="px-5 py-3 text-xs">{item.raisedByName}</td>
                    <td className="px-5 py-3 text-xs">{item.assignedToName || '—'}</td>
                    <td className="px-5 py-3 text-xs">{fmtDate(item.createdAt)}</td>
                    <td className="px-5 py-3 text-right">
                      <Button size="sm" variant="ghost" icon={<MessageSquareReply className="h-4 w-4" />} onClick={() => setDetail(item)} />
                    </td>
                  </tr>
                )) : <tr><td colSpan={9}><EmptyState icon={<LifeBuoy className="h-6 w-6" />} title="No tickets" hint={canRaise ? 'Raise your first support ticket.' : 'No helpdesk tickets yet.'} /></td></tr>}
            </tbody>
          </table>
        </div>
      </Card>

      {open && (
        <Modal open={open} onClose={() => setOpen(false)} title="Raise a Support Ticket"
          footer={<><Button variant="secondary" onClick={() => setOpen(false)}>Cancel</Button><Button loading={create.isPending} disabled={!form.subject || !form.description} onClick={() => create.mutate()}>Submit Ticket</Button></>}>
          <div className="grid sm:grid-cols-2 gap-4">
            <Field label="Subject" required><Input value={form.subject} onChange={(e) => setForm({ ...form, subject: e.target.value })} placeholder="Brief summary of the issue" /></Field>
            <Field label="Category">
              <Select value={form.category} onChange={(e) => setForm({ ...form, category: e.target.value })}>
                <option value="General">General</option>
                <option value="Technical">Technical</option>
                <option value="Access">Access</option>
                <option value="Bug">Bug</option>
                <option value="Feature">Feature</option>
                <option value="Other">Other</option>
              </Select>
            </Field>
            <Field label="Priority">
              <Select value={form.priority} onChange={(e) => setForm({ ...form, priority: e.target.value })}>
                <option value="Low">Low</option>
                <option value="Medium">Medium</option>
                <option value="High">High</option>
                <option value="Critical">Critical</option>
              </Select>
            </Field>
            <div className="sm:col-span-2"><Field label="Description" required><Textarea value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></Field></div>
          </div>
        </Modal>
      )}

      {detail && (
        <Modal open={!!detail} onClose={() => { setDetail(null); setUpdateForm({ status: '', resolution: '' }) }} title={`Ticket #${detail.id}`}
          footer={<><Button variant="secondary" onClick={() => { setDetail(null); setUpdateForm({ status: '', resolution: '' }) }}>Close</Button>
            {canManage && <Button icon={<MessageSquareReply className="h-4 w-4" />} loading={update.isPending} disabled={!updateForm.status && !updateForm.resolution} onClick={() => update.mutate()}>Update</Button>}</>}>
          <div className="space-y-4">
            <div className="grid sm:grid-cols-2 gap-4">
              <div><span className="av-label block mb-1">Subject</span><p className="text-sm">{detail.subject}</p></div>
              <div><span className="av-label block mb-1">Category</span><p className="text-sm">{detail.category}</p></div>
              <div><span className="av-label block mb-1">Priority</span><StatusPill value={detail.priority} /></div>
              <div><span className="av-label block mb-1">Status</span><StatusPill value={detail.status} /></div>
              <div><span className="av-label block mb-1">Raised By</span><p className="text-sm">{detail.raisedByName}</p></div>
              <div><span className="av-label block mb-1">Assigned To</span><p className="text-sm">{detail.assignedToName || '—'}</p></div>
              <div className="sm:col-span-2"><span className="av-label block mb-1">Description</span><p className="text-sm whitespace-pre-wrap">{detail.description}</p></div>
              {detail.resolution && <div className="sm:col-span-2"><span className="av-label block mb-1">Resolution</span><p className="text-sm whitespace-pre-wrap">{detail.resolution}</p></div>}
              <div><span className="av-label block mb-1">Created</span><p className="text-sm">{fmtDateTime(detail.createdAt)}</p></div>
              {detail.resolvedAt && <div><span className="av-label block mb-1">Resolved</span><p className="text-sm">{fmtDateTime(detail.resolvedAt)}</p></div>}
              {detail.closedAt && <div><span className="av-label block mb-1">Closed</span><p className="text-sm">{fmtDateTime(detail.closedAt)}</p></div>}
            </div>
            {canManage && (
              <div className="border-t border-[var(--line)] pt-4 space-y-4">
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Update Ticket</p>
                <div className="grid sm:grid-cols-2 gap-4">
                  <Field label="Status">
                    <Select value={updateForm.status} onChange={(e) => setUpdateForm({ ...updateForm, status: e.target.value })}>
                      <option value="">— keep current —</option>
                      {STATUS_OPTS.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
                    </Select>
                  </Field>
                  <Field label="Resolution / Note"><Textarea value={updateForm.resolution} onChange={(e) => setUpdateForm({ ...updateForm, resolution: e.target.value })} /></Field>
                </div>
              </div>
            )}
          </div>
        </Modal>
      )}
    </div>
  )
}
