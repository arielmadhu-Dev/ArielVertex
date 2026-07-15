import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ClipboardCheck, Plus, CalendarPlus, Video, CheckCircle2, Send, Check, X } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import { useEnums, useEmployees } from '../lib/hooks'
import type { ReviewRequest } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, StatusPill, Badge, Avatar, Button, EmptyState } from '../ui/primitives'
import { Stars } from '../ui/widgets'
import { Modal } from '../ui/Modal'
import { Field, Input, Select, Textarea } from '../ui/form'
import { useToast } from '../ui/Toast'
import { asArray, asText, fmtDate, fmtDateTime, nice } from '../ui/util'
import { P } from '../components/nav'

function isWorkingTeamsUrl(value?: string | null) {
  if (!value || value.toLowerCase().includes('av-placeholder')) return false
  try {
    const url = new URL(value)
    return url.protocol === 'https:' && url.hostname.toLowerCase() === 'teams.microsoft.com'
  } catch {
    return false
  }
}

export default function Reviews() {
  const { user, has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const enums = useEnums()
  const employees = useEmployees()
  const [createOpen, setCreateOpen] = useState(false)
  const [groupOpen, setGroupOpen] = useState(false)
  const [schedule, setSchedule] = useState<ReviewRequest | null>(null)
  const [outcome, setOutcome] = useState<ReviewRequest | null>(null)

  const { data } = useQuery({ queryKey: ['reviews'], queryFn: async () => (await api.get<ReviewRequest[]>('/review-requests')).data })
  const projects = useQuery({
    queryKey: ['projects', 'options', 'reviews'],
    queryFn: async () => asArray((await api.get('/projects', { params: { pageSize: 50 } })).data?.items) as any[],
  })

  const invalidate = () => qc.invalidateQueries({ queryKey: ['reviews'] })
  const reviews = asArray(data)

  const respond = useMutation({
    mutationFn: ({ id, response }: { id: number; response: string }) => api.post(`/review-requests/${id}/respond`, { response }),
    onSuccess: (_r, v) => { push(`Review ${v.response.toLowerCase()}`); invalidate() },
    onError: (e: any) => push(apiError(e), 'error'),
  })
  const respTone: Record<string, 'good' | 'danger' | 'warn' | 'info'> = { Accepted: 'good', Declined: 'danger', Tentative: 'warn', NoResponse: 'info' }

  return (
    <div>
      <PageHeader title="Reviews" subtitle="Project & code reviews — request, schedule, and record outcomes" icon={<ClipboardCheck className="h-5 w-5" />}
        actions={has(P.ReviewsRequest) ? (
          <div className="flex items-center gap-2">
            <Button variant="secondary" icon={<Plus className="h-4 w-4" />} onClick={() => setCreateOpen(true)}>Request review</Button>
            <Button icon={<CalendarPlus className="h-4 w-4" />} onClick={() => setGroupOpen(true)}>Schedule reviews</Button>
          </div>
        ) : undefined} />

      {reviews.length ? (
        <div className="space-y-3">
          {reviews.map((r) => {
            const canSchedule = has(P.ReviewsSchedule) && r.status !== 'Completed'
            const canSubmit = has(P.ReviewsSubmit) && r.assignedToId === user?.id && r.status !== 'Completed'
            const isSubject = r.subjectUserId === user?.id
            const resp = r.meeting?.responseStatus
            const canRespond = isSubject && !!r.meeting && r.status !== 'Completed'
            const joinUrl = r.meeting?.teamsJoinUrl
            const hasWorkingJoinUrl = isWorkingTeamsUrl(joinUrl)
            return (
              <Card key={r.id} className="p-4">
                <div className="flex flex-wrap items-center gap-4">
                  <Avatar name={r.subjectName} color={r.avatarColor} size={44} />
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2 flex-wrap">
                      <p className="font-bold">{asText(r.subjectName, 'Unknown employee')}</p>
                      <Badge t="info">{nice(r.reviewType)}</Badge>
                      <StatusPill value={r.status} />
                    </div>
                    <p className="text-xs text-slate-500 mt-0.5">{r.projectName} · Requested by {r.requestedByName}{r.assignedToName ? ` · Reviewer: ${r.assignedToName}` : ''}{r.dueDate ? ` · Due ${fmtDate(r.dueDate)}` : ''}</p>
                    {r.notes && <p className="text-sm mt-1 text-slate-600 dark:text-slate-300">{r.notes}</p>}
                    {r.meeting && (
                      <div className="mt-2 flex items-center gap-2 flex-wrap text-xs">
                        <div className="inline-flex items-center gap-2 rounded-lg bg-brand-50 dark:bg-brand-900/40 px-2.5 py-1.5">
                          <span className="font-semibold">{fmtDateTime(r.meeting.scheduledAt)}</span>
                          {hasWorkingJoinUrl && <a href={joinUrl!} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1 rounded-md bg-brand-500 px-2 py-1 text-white font-semibold hover:bg-brand-600"><Video className="h-3.5 w-3.5" />Join Teams</a>}
                          {!!joinUrl && !hasWorkingJoinUrl && <Badge t="danger">Calendar link unavailable — reschedule</Badge>}
                        </div>
                        {resp && resp !== 'NoResponse' && <Badge t={respTone[resp]}>{resp}</Badge>}
                        {resp === 'NoResponse' && !isSubject && <Badge t="info">Awaiting response</Badge>}
                      </div>
                    )}
                  </div>
                  <div className="flex items-center gap-2">
                    {r.hasOutcome && <span className="inline-flex items-center gap-1 text-emerald-600 text-sm font-semibold"><CheckCircle2 className="h-4 w-4" />Outcome recorded</span>}
                    {canRespond && (
                      <>
                        <Button size="sm" variant={resp === 'Accepted' ? 'primary' : 'secondary'} icon={<Check className="h-4 w-4" />} loading={respond.isPending} onClick={() => respond.mutate({ id: r.id, response: 'Accepted' })}>Accept</Button>
                        <Button size="sm" variant={resp === 'Declined' ? 'danger' : 'subtle'} icon={<X className="h-4 w-4" />} loading={respond.isPending} onClick={() => respond.mutate({ id: r.id, response: 'Declined' })}>Decline</Button>
                      </>
                    )}
                    {canSchedule && <Button size="sm" variant="secondary" icon={<CalendarPlus className="h-4 w-4" />} onClick={() => setSchedule(r)}>{r.meeting ? 'Reschedule' : 'Schedule'}</Button>}
                    {canSubmit && !r.hasOutcome && <Button size="sm" icon={<Send className="h-4 w-4" />} onClick={() => setOutcome(r)}>Submit outcome</Button>}
                  </div>
                </div>
              </Card>
            )
          })}
        </div>
      ) : <Card><EmptyState icon={<ClipboardCheck className="h-6 w-6" />} title="No review requests" hint="HR can request project or code reviews for employees." /></Card>}

      {/* Create */}
      <CreateReviewModal open={createOpen} onClose={() => setCreateOpen(false)} projects={projects.data ?? []} employees={employees.data ?? []} reviewTypes={enums.data?.reviewTypes ?? []} onDone={() => { invalidate(); setCreateOpen(false) }} push={push} />
      {/* Group schedule (multi-employee, one calendar event) */}
      <GroupScheduleModal open={groupOpen} onClose={() => setGroupOpen(false)} projects={projects.data ?? []} employees={employees.data ?? []} reviewTypes={enums.data?.reviewTypes ?? []} onDone={() => { invalidate(); setGroupOpen(false) }} push={push} />
      {/* Schedule */}
      {schedule && <ScheduleModal req={schedule} onClose={() => setSchedule(null)} onDone={() => { invalidate(); setSchedule(null) }} push={push} />}
      {/* Outcome */}
      {outcome && <OutcomeModal req={outcome} onClose={() => setOutcome(null)} onDone={() => { invalidate(); setOutcome(null) }} push={push} />}
    </div>
  )
}

function CreateReviewModal({ open, onClose, projects, employees, reviewTypes, onDone, push }: any) {
  const [form, setForm] = useState({ projectId: 0, subjectUserId: 0, assignedToId: 0, reviewType: 'ProjectReview', notes: '', dueDate: '' })
  const projectOptions = asArray(projects)
  const employeeOptions = asArray(employees)
  const reviewTypeOptions = asArray(reviewTypes)
  const create = useMutation({
    mutationFn: () => api.post('/review-requests', { ...form, assignedToId: form.assignedToId || null, dueDate: form.dueDate || null }),
    onSuccess: () => { push('Review requested — reviewer notified'); onDone() },
    onError: (e: any) => push(apiError(e), 'error'),
  })
  return (
    <Modal open={open} onClose={onClose} title="Request a review" subtitle="Ask a PM/PC or Tech Lead to review an employee"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={create.isPending} disabled={!form.projectId || !form.subjectUserId} onClick={() => create.mutate()}>Send request</Button></>}>
      <div className="grid sm:grid-cols-2 gap-4">
        <Field label="Project" required><Select value={form.projectId} onChange={(e) => setForm({ ...form, projectId: Number(e.target.value) })}><option value={0}>Select…</option>{projectOptions.map((p: any) => <option key={p.id} value={p.id}>{p.name}</option>)}</Select></Field>
        <Field label="Review type"><Select value={form.reviewType} onChange={(e) => setForm({ ...form, reviewType: e.target.value })}>{reviewTypeOptions.map((o: any) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select></Field>
        <Field label="Employee" required><Select value={form.subjectUserId} onChange={(e) => setForm({ ...form, subjectUserId: Number(e.target.value) })}><option value={0}>Select…</option>{employeeOptions.map((e: any) => <option key={e.id} value={e.id}>{e.name}</option>)}</Select></Field>
        <Field label="Assign reviewer"><Select value={form.assignedToId} onChange={(e) => setForm({ ...form, assignedToId: Number(e.target.value) })}><option value={0}>Later…</option>{employeeOptions.map((e: any) => <option key={e.id} value={e.id}>{e.name}</option>)}</Select></Field>
        <Field label="Due date"><Input type="date" value={form.dueDate} onChange={(e) => setForm({ ...form, dueDate: e.target.value })} /></Field>
        <div className="sm:col-span-2"><Field label="Notes"><Textarea value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} /></Field></div>
      </div>
    </Modal>
  )
}

function ScheduleModal({ req, onClose, onDone, push }: any) {
  const [form, setForm] = useState({ title: `${nice(req.reviewType)} - ${asText(req.subjectName, 'Unknown employee')}`, scheduledAt: '', durationMinutes: 45, attendees: '' })
  const schedule = useMutation({
    mutationFn: () => api.post(`/review-requests/${req.id}/schedule`, form),
    onSuccess: (r: any) => { push(r.data.graphLive ? 'Calendar invite sent — Teams link is ready' : 'Review scheduled without calendar integration'); onDone() },
    onError: (e: any) => push(apiError(e), 'error'),
  })
  return (
    <Modal open onClose={onClose} title="Schedule review" subtitle="One click creates the Outlook invite + Teams link"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={schedule.isPending} disabled={!form.scheduledAt} onClick={() => schedule.mutate()}>Schedule</Button></>}>
      <div className="grid sm:grid-cols-2 gap-4">
        <div className="sm:col-span-2"><Field label="Meeting title" required><Input value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} /></Field></div>
        <Field label="Date & time" required><Input type="datetime-local" value={form.scheduledAt} onChange={(e) => setForm({ ...form, scheduledAt: e.target.value })} /></Field>
        <Field label="Duration (min)"><Input type="number" value={form.durationMinutes} onChange={(e) => setForm({ ...form, durationMinutes: Number(e.target.value) })} /></Field>
        <div className="sm:col-span-2"><Field label="Extra attendees (optional)" hint="The review subject, assigned reviewer, and scheduling PM/PC are included automatically. Add any other emails separated by commas."><Input value={form.attendees} onChange={(e) => setForm({ ...form, attendees: e.target.value })} placeholder="additional.person@company.com" /></Field></div>
      </div>
    </Modal>
  )
}

function GroupScheduleModal({ open, onClose, projects, employees, reviewTypes, onDone, push }: any) {
  const [form, setForm] = useState({ projectId: 0, reviewType: 'CodeReview', assignedToId: 0, title: '', scheduledAt: '', durationMinutes: 45, notes: '' })
  const [selected, setSelected] = useState<number[]>([])
  const toggle = (id: number) => setSelected((s) => s.includes(id) ? s.filter((x) => x !== id) : [...s, id])
  const typeLabel = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2')

  const submit = useMutation({
    mutationFn: () => api.post('/review-requests/schedule', {
      projectId: form.projectId, reviewType: form.reviewType, assignedToId: form.assignedToId || null,
      subjectUserIds: selected, title: form.title, scheduledAt: form.scheduledAt,
      durationMinutes: form.durationMinutes, notes: form.notes,
    }),
    onSuccess: (r: any) => { push(`Scheduled for ${r.data.count} employee(s)${r.data.graphLive ? ' — Teams/Outlook calendar invites sent' : ' — without calendar integration'}`); setSelected([]); onDone() },
    onError: (e: any) => push(apiError(e), 'error'),
  })

  const canSubmit = !!(form.projectId && selected.length && form.title && form.scheduledAt)
  return (
    <Modal open={open} onClose={onClose} title="Schedule reviews" subtitle="Select one or more employees — one calendar invite goes to everyone"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={submit.isPending} disabled={!canSubmit} onClick={() => submit.mutate()}>{selected.length ? `Schedule (${selected.length})` : 'Schedule'}</Button></>}>
      <div className="grid sm:grid-cols-2 gap-4">
        <Field label="Project" required><Select value={form.projectId} onChange={(e) => setForm({ ...form, projectId: Number(e.target.value) })}><option value={0}>Select…</option>{projects.map((p: any) => <option key={p.id} value={p.id}>{p.name}</option>)}</Select></Field>
        <Field label="Review type"><Select value={form.reviewType} onChange={(e) => setForm({ ...form, reviewType: e.target.value, title: form.title || `${typeLabel(e.target.value)} session` })}>{reviewTypes.map((o: any) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select></Field>
        <div className="sm:col-span-2">
          <Field label={`Employees · ${selected.length} selected`} required>
            <div className="max-h-44 overflow-y-auto rounded-xl border border-[var(--line)] divide-y divide-[var(--line)]">
              {employees.map((e: any) => (
                <label key={e.id} className="flex items-center gap-3 px-3 py-2 text-sm cursor-pointer hover:bg-slate-50 dark:hover:bg-navy-600">
                  <input type="checkbox" checked={selected.includes(e.id)} onChange={() => toggle(e.id)} className="h-4 w-4 accent-brand-600" />
                  <span className="font-medium">{e.name}</span><span className="text-xs text-slate-500">{e.designation}</span>
                </label>
              ))}
              {!employees.length && <p className="px-3 py-4 text-xs text-slate-500">No employees available.</p>}
            </div>
          </Field>
        </div>
        <div className="sm:col-span-2"><Field label="Meeting title" required><Input value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} placeholder="e.g. Q3 Code Review" /></Field></div>
        <Field label="Date & time" required><Input type="datetime-local" value={form.scheduledAt} onChange={(e) => setForm({ ...form, scheduledAt: e.target.value })} /></Field>
        <Field label="Duration (min)"><Input type="number" value={form.durationMinutes} onChange={(e) => setForm({ ...form, durationMinutes: Number(e.target.value) })} /></Field>
        <Field label="Assign reviewer"><Select value={form.assignedToId} onChange={(e) => setForm({ ...form, assignedToId: Number(e.target.value) })}><option value={0}>Later…</option>{employees.map((e: any) => <option key={e.id} value={e.id}>{e.name}</option>)}</Select></Field>
        <div className="sm:col-span-2"><Field label="Notes"><Textarea value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} /></Field></div>
      </div>
    </Modal>
  )
}

function OutcomeModal({ req, onClose, onDone, push }: any) {
  const isCode = req.reviewType === 'CodeReview'
  const [r1, setR1] = useState(4); const [r2, setR2] = useState(4); const [r3, setR3] = useState(4)
  const [strengths, setStrengths] = useState(''); const [observations, setObservations] = useState('')
  const [actionItems, setActionItems] = useState(''); const [blockers, setBlockers] = useState('')

  const submit = useMutation({
    mutationFn: () => isCode
      ? api.post(`/review-requests/${req.id}/code-review`, { codeQualityRating: r1, architectureRating: r2, testingRating: r3, strengths, observations, actionItems, blockers })
      : api.post(`/review-requests/${req.id}/project-review`, { deliveryRating: r1, ownershipRating: r2, collaborationRating: r3, strengths, observations, actionItems }),
    onSuccess: () => { push('Review outcome submitted'); onDone() },
    onError: (e: any) => push(apiError(e), 'error'),
  })

  const labels = isCode ? ['Code quality', 'Architecture', 'Testing'] : ['Delivery', 'Ownership', 'Collaboration']
  const setters = [setR1, setR2, setR3]; const vals = [r1, r2, r3]
  return (
    <Modal open onClose={onClose} title={`Submit ${isCode ? 'code' : 'project'} review`} subtitle={`For ${req.subjectName} · ${req.projectName}`}
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={submit.isPending} onClick={() => submit.mutate()}>Submit outcome</Button></>}>
      <div className="space-y-4">
        <div className="grid grid-cols-3 gap-3">
          {labels.map((l, i) => (
            <div key={l} className="rounded-xl border border-[var(--line)] p-3 text-center">
              <p className="text-xs font-semibold text-slate-500 mb-1.5">{l}</p>
              <Stars value={vals[i]} onChange={setters[i]} />
            </div>
          ))}
        </div>
        <Field label="Strengths"><Textarea value={strengths} onChange={(e) => setStrengths(e.target.value)} /></Field>
        <Field label="Observations"><Textarea value={observations} onChange={(e) => setObservations(e.target.value)} /></Field>
        {isCode && <Field label="Blockers"><Textarea value={blockers} onChange={(e) => setBlockers(e.target.value)} /></Field>}
        <Field label="Action items"><Textarea value={actionItems} onChange={(e) => setActionItems(e.target.value)} /></Field>
      </div>
    </Modal>
  )
}
