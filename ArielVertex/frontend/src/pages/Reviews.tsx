import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ClipboardCheck, Plus, CalendarPlus, Video, CheckCircle2, Send } from 'lucide-react'
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
import { fmtDate, fmtDateTime } from '../ui/util'
import { P } from '../components/nav'

const nice = (s: string) => s.replace(/([a-z])([A-Z])/g, '$1 $2')

export default function Reviews() {
  const { user, has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const enums = useEnums()
  const employees = useEmployees()
  const [createOpen, setCreateOpen] = useState(false)
  const [schedule, setSchedule] = useState<ReviewRequest | null>(null)
  const [outcome, setOutcome] = useState<ReviewRequest | null>(null)

  const { data } = useQuery({ queryKey: ['reviews'], queryFn: async () => (await api.get<ReviewRequest[]>('/review-requests')).data })
  const projects = useQuery({ queryKey: ['projects', ''], queryFn: async () => (await api.get('/projects', { params: { pageSize: 50 } })).data.items as any[] })

  const invalidate = () => qc.invalidateQueries({ queryKey: ['reviews'] })

  return (
    <div>
      <PageHeader title="Reviews" subtitle="Project & code reviews — request, schedule, and record outcomes" icon={<ClipboardCheck className="h-5 w-5" />}
        actions={has(P.ReviewsRequest) ? <Button icon={<Plus className="h-4 w-4" />} onClick={() => setCreateOpen(true)}>Request review</Button> : undefined} />

      {data?.length ? (
        <div className="space-y-3">
          {data.map((r) => {
            const canSchedule = has(P.ReviewsSchedule) && r.status !== 'Completed'
            const canSubmit = has(P.ReviewsSubmit) && r.assignedToId === user?.id && r.status !== 'Completed'
            return (
              <Card key={r.id} className="p-4">
                <div className="flex flex-wrap items-center gap-4">
                  <Avatar name={r.subjectName} color={r.avatarColor} size={44} />
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2 flex-wrap">
                      <p className="font-bold">{r.subjectName}</p>
                      <Badge t="info">{nice(r.reviewType)}</Badge>
                      <StatusPill value={r.status} />
                    </div>
                    <p className="text-xs text-slate-500 mt-0.5">{r.projectName} · Requested by {r.requestedByName}{r.assignedToName ? ` · Reviewer: ${r.assignedToName}` : ''}{r.dueDate ? ` · Due ${fmtDate(r.dueDate)}` : ''}</p>
                    {r.notes && <p className="text-sm mt-1 text-slate-600 dark:text-slate-300">{r.notes}</p>}
                    {r.meeting && (
                      <div className="mt-2 inline-flex items-center gap-2 rounded-lg bg-brand-50 dark:bg-brand-900/40 px-2.5 py-1.5 text-xs">
                        <span className="font-semibold">{fmtDateTime(r.meeting.scheduledAt)}</span>
                        {r.meeting.teamsJoinUrl && <a href={r.meeting.teamsJoinUrl} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-brand-600 font-semibold"><Video className="h-3.5 w-3.5" />Join</a>}
                      </div>
                    )}
                  </div>
                  <div className="flex items-center gap-2">
                    {r.hasOutcome && <span className="inline-flex items-center gap-1 text-emerald-600 text-sm font-semibold"><CheckCircle2 className="h-4 w-4" />Outcome recorded</span>}
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
      {/* Schedule */}
      {schedule && <ScheduleModal req={schedule} onClose={() => setSchedule(null)} onDone={() => { invalidate(); setSchedule(null) }} push={push} />}
      {/* Outcome */}
      {outcome && <OutcomeModal req={outcome} onClose={() => setOutcome(null)} onDone={() => { invalidate(); setOutcome(null) }} push={push} />}
    </div>
  )
}

function CreateReviewModal({ open, onClose, projects, employees, reviewTypes, onDone, push }: any) {
  const [form, setForm] = useState({ projectId: 0, subjectUserId: 0, assignedToId: 0, reviewType: 'ProjectReview', notes: '', dueDate: '' })
  const create = useMutation({
    mutationFn: () => api.post('/review-requests', { ...form, assignedToId: form.assignedToId || null, dueDate: form.dueDate || null }),
    onSuccess: () => { push('Review requested — reviewer notified'); onDone() },
    onError: (e: any) => push(apiError(e), 'error'),
  })
  return (
    <Modal open={open} onClose={onClose} title="Request a review" subtitle="Ask a PM/PC or Tech Lead to review an employee"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={create.isPending} disabled={!form.projectId || !form.subjectUserId} onClick={() => create.mutate()}>Send request</Button></>}>
      <div className="grid sm:grid-cols-2 gap-4">
        <Field label="Project" required><Select value={form.projectId} onChange={(e) => setForm({ ...form, projectId: Number(e.target.value) })}><option value={0}>Select…</option>{projects.map((p: any) => <option key={p.id} value={p.id}>{p.name}</option>)}</Select></Field>
        <Field label="Review type"><Select value={form.reviewType} onChange={(e) => setForm({ ...form, reviewType: e.target.value })}>{reviewTypes.map((o: any) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select></Field>
        <Field label="Employee" required><Select value={form.subjectUserId} onChange={(e) => setForm({ ...form, subjectUserId: Number(e.target.value) })}><option value={0}>Select…</option>{employees.map((e: any) => <option key={e.id} value={e.id}>{e.name}</option>)}</Select></Field>
        <Field label="Assign reviewer"><Select value={form.assignedToId} onChange={(e) => setForm({ ...form, assignedToId: Number(e.target.value) })}><option value={0}>Later…</option>{employees.map((e: any) => <option key={e.id} value={e.id}>{e.name}</option>)}</Select></Field>
        <Field label="Due date"><Input type="date" value={form.dueDate} onChange={(e) => setForm({ ...form, dueDate: e.target.value })} /></Field>
        <div className="sm:col-span-2"><Field label="Notes"><Textarea value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} /></Field></div>
      </div>
    </Modal>
  )
}

function ScheduleModal({ req, onClose, onDone, push }: any) {
  const [form, setForm] = useState({ title: `${req.reviewType.replace(/([a-z])([A-Z])/g, '$1 $2')} — ${req.subjectName}`, scheduledAt: '', durationMinutes: 45, attendees: '' })
  const schedule = useMutation({
    mutationFn: () => api.post(`/review-requests/${req.id}/schedule`, form),
    onSuccess: (r: any) => { push(r.data.graphLive ? 'Scheduled in Teams/Outlook' : 'Scheduled — Teams link generated'); onDone() },
    onError: (e: any) => push(apiError(e), 'error'),
  })
  return (
    <Modal open onClose={onClose} title="Schedule review" subtitle="One click creates the Outlook invite + Teams link"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={schedule.isPending} disabled={!form.scheduledAt} onClick={() => schedule.mutate()}>Schedule</Button></>}>
      <div className="grid sm:grid-cols-2 gap-4">
        <div className="sm:col-span-2"><Field label="Meeting title" required><Input value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} /></Field></div>
        <Field label="Date & time" required><Input type="datetime-local" value={form.scheduledAt} onChange={(e) => setForm({ ...form, scheduledAt: e.target.value })} /></Field>
        <Field label="Duration (min)"><Input type="number" value={form.durationMinutes} onChange={(e) => setForm({ ...form, durationMinutes: Number(e.target.value) })} /></Field>
        <div className="sm:col-span-2"><Field label="Attendees" hint="Comma-separated emails"><Input value={form.attendees} onChange={(e) => setForm({ ...form, attendees: e.target.value })} /></Field></div>
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
