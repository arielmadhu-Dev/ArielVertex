import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  NotebookPen, Plus, Sparkles, Wand2, CheckCircle2, Send, Pencil, Trash2, Users, CalendarClock, MapPin, SpellCheck,
} from 'lucide-react'
import { api, apiError } from '../lib/api'
import { PageHeader } from '../components/PageHeader'
import { Card, StatusPill, Badge, Button, EmptyState, Skeleton } from '../ui/primitives'
import { Modal } from '../ui/Modal'
import { Field, Input, Textarea } from '../ui/form'
import { useToast } from '../ui/Toast'
import { fmtDateTime } from '../ui/util'

interface Minute {
  id: number; title: string; meetingDate: string; location?: string; attendees: string[]
  rawNotes: string; minutesText: string; status: string; statusLabel: string; generatedByAi: boolean
  createdById: number; createdByName: string; approvedByName?: string; approvedAt?: string
  sentAt?: string; sentCount: number; createdAt: string; canManage: boolean
}

const statusTone = (s: string): any =>
  s === 'Sent' ? 'good' : s === 'Approved' ? 'info' : s === 'Preview' ? 'warn' : 'neutral'

export default function Meetings() {
  const { push } = useToast()
  const qc = useQueryClient()
  const [openId, setOpenId] = useState<number | 'new' | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['minutes'],
    queryFn: async () => (await api.get<Minute[]>('/minutes')).data,
  })
  const invalidate = () => qc.invalidateQueries({ queryKey: ['minutes'] })

  return (
    <div>
      <PageHeader title="Meetings & Minutes"
        subtitle="Capture notes, auto-generate the minutes, preview, then send to all attendees — grammar corrected automatically"
        icon={<NotebookPen className="h-5 w-5" />}
        actions={<Button icon={<Plus className="h-4 w-4" />} onClick={() => setOpenId('new')}>New minutes</Button>} />

      {isLoading ? <div className="space-y-3">{[...Array(3)].map((_, i) => <Skeleton key={i} className="h-24" />)}</div>
        : data?.length ? (
          <div className="grid gap-3 lg:grid-cols-2">
            {data.map((m) => (
              <Card key={m.id} className="p-5 cursor-pointer hover:border-brand-400 transition" onClick={() => setOpenId(m.id)}>
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="font-bold truncate">{m.title}</p>
                    <p className="text-xs text-slate-400 mt-1 flex items-center gap-3 flex-wrap">
                      <span className="inline-flex items-center gap-1"><CalendarClock className="h-3.5 w-3.5" />{fmtDateTime(m.meetingDate)}</span>
                      <span className="inline-flex items-center gap-1"><Users className="h-3.5 w-3.5" />{m.attendees.length} attendee(s)</span>
                      {m.location && <span className="inline-flex items-center gap-1"><MapPin className="h-3.5 w-3.5" />{m.location}</span>}
                    </p>
                  </div>
                  <StatusPill value={m.status} />
                </div>
                <div className="mt-3 flex items-center gap-2 flex-wrap">
                  <Badge t={statusTone(m.status)}>{m.statusLabel}</Badge>
                  {m.minutesText && <Badge t={m.generatedByAi ? 'info' : 'neutral'}>{m.generatedByAi ? <><Sparkles className="h-3.5 w-3.5" />AI-generated</> : 'Auto-generated'}</Badge>}
                  {m.sentCount > 0 && <span className="text-xs text-slate-400">Sent {m.sentCount}×{m.sentAt ? ` · ${fmtDateTime(m.sentAt)}` : ''}</span>}
                </div>
              </Card>
            ))}
          </div>
        ) : <Card><EmptyState icon={<NotebookPen className="h-6 w-6" />} title="No minutes yet"
          hint="Capture your meeting notes and the system will draft the minutes for you to review and send."
          action={<Button icon={<Plus className="h-4 w-4" />} onClick={() => setOpenId('new')}>New minutes</Button>} /></Card>}

      {openId === 'new' && <CreateModal onClose={() => setOpenId(null)} onCreated={(id: number) => { invalidate(); setOpenId(id) }} push={push} />}
      {typeof openId === 'number' && <EditorModal id={openId} onClose={() => { invalidate(); setOpenId(null) }} onChanged={invalidate} push={push} />}
    </div>
  )
}

function toLocalInput(iso: string) {
  const d = new Date(iso); const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`
}

function CreateModal({ onClose, onCreated, push }: any) {
  const [f, setF] = useState({ title: '', meetingDate: toLocalInput(new Date().toISOString()), location: '', attendees: '', rawNotes: '' })
  const create = useMutation({
    mutationFn: () => api.post('/minutes', f),
    onSuccess: (r) => { push('Draft saved — now generate the minutes'); onCreated(r.data.id) },
    onError: (e: any) => push(apiError(e), 'error'),
  })
  return (
    <Modal open onClose={onClose} size="lg" title="New meeting minutes" subtitle="Capture the essentials and your notes — you'll generate and review the minutes next"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button>
        <Button loading={create.isPending} disabled={!f.title || !f.rawNotes} onClick={() => create.mutate()}>Save & continue</Button></>}>
      <div className="space-y-4">
        <div className="grid sm:grid-cols-2 gap-4">
          <Field label="Meeting title" required><Input value={f.title} onChange={(e) => setF({ ...f, title: e.target.value })} placeholder="e.g. MIB Portal — Weekly Sync" /></Field>
          <Field label="Date & time"><Input type="datetime-local" value={f.meetingDate} onChange={(e) => setF({ ...f, meetingDate: e.target.value })} /></Field>
        </div>
        <Field label="Location"><Input value={f.location} onChange={(e) => setF({ ...f, location: e.target.value })} placeholder="Teams, Meeting Room 2, …" /></Field>
        <Field label="Attendees" hint="Email addresses, comma or new-line separated. Internal attendees are notified in the portal."><Textarea value={f.attendees} onChange={(e) => setF({ ...f, attendees: e.target.value })} className="min-h-[64px]" placeholder="sudhir@arielsoftwares.in, nikhil@arielsoftwares.in" /></Field>
        <Field label="Meeting notes" required hint="Rough notes are fine — the system structures and grammar-corrects them for you."><Textarea value={f.rawNotes} onChange={(e) => setF({ ...f, rawNotes: e.target.value })} className="min-h-[140px]" placeholder={"dashboard api done\nkyc blocked, waiting on client\nagreed to demo next thursday\npriya to prep test data by wed"} /></Field>
      </div>
    </Modal>
  )
}

function EditorModal({ id, onClose, onChanged, push }: any) {
  const { data: m, isLoading, refetch } = useQuery({
    queryKey: ['minutes', id],
    queryFn: async () => (await api.get<Minute>(`/minutes/${id}`)).data,
  })
  const [details, setDetails] = useState<any>(null)
  const [minutesText, setMinutesText] = useState('')
  // hydrate local editable state once loaded / after actions
  const hydrate = (x: Minute) => {
    setDetails({ title: x.title, meetingDate: toLocalInput(x.meetingDate), location: x.location ?? '', attendees: x.attendees.join(', '), rawNotes: x.rawNotes })
    setMinutesText(x.minutesText)
  }
  if (m && details === null) hydrate(m)

  const afterServer = (x: Minute) => { hydrate(x); onChanged() }

  const save = useMutation({
    mutationFn: () => api.put(`/minutes/${id}`, { ...details, minutesText }),
    onSuccess: (r) => { push('Saved'); afterServer(r.data) },
    onError: (e: any) => push(apiError(e), 'error'),
  })
  const generate = useMutation({
    mutationFn: async () => { await api.put(`/minutes/${id}`, { ...details, minutesText }); return api.post(`/minutes/${id}/generate`) },
    onSuccess: (r) => { push(r.data.generatedByAi ? 'Minutes drafted with AI' : 'Minutes drafted'); afterServer(r.data) },
    onError: (e: any) => push(apiError(e), 'error'),
  })
  const approve = useMutation({
    mutationFn: () => api.post(`/minutes/${id}/approve`),
    onSuccess: (r) => { push('Approved — ready to send'); afterServer(r.data) },
    onError: (e: any) => push(apiError(e), 'error'),
  })
  const send = useMutation({
    mutationFn: () => api.post(`/minutes/${id}/send`),
    onSuccess: (r) => { push(`Sent to ${r.data.attendees} attendee(s)`); afterServer(r.data.minutes) },
    onError: (e: any) => push(apiError(e), 'error'),
  })
  const del = useMutation({
    mutationFn: () => api.delete(`/minutes/${id}`),
    onSuccess: () => { push('Deleted'); onClose() },
    onError: (e: any) => push(apiError(e), 'error'),
  })

  const busy = save.isPending || generate.isPending || approve.isPending || send.isPending
  const hasPreview = !!minutesText && (m?.status === 'Preview' || m?.status === 'Approved' || m?.status === 'Sent')
  const canApprove = m?.status === 'Preview'
  const canSend = m?.status === 'Approved' || m?.status === 'Sent'

  return (
    <Modal open onClose={onClose} size="lg"
      title={m ? m.title : 'Minutes'}
      subtitle={m ? m.statusLabel : undefined}
      footer={
        <div className="flex items-center justify-between w-full gap-2">
          <Button variant="ghost" icon={<Trash2 className="h-4 w-4" />} onClick={() => del.mutate()} loading={del.isPending}>Delete</Button>
          <div className="flex items-center gap-2">
            <Button variant="secondary" icon={<Pencil className="h-4 w-4" />} onClick={() => save.mutate()} loading={save.isPending} disabled={busy || !details}>Save</Button>
            <Button icon={<Wand2 className="h-4 w-4" />} onClick={() => generate.mutate()} loading={generate.isPending} disabled={busy || !details?.rawNotes}>
              {hasPreview ? 'Regenerate' : 'Generate minutes'}
            </Button>
            {canApprove && <Button icon={<CheckCircle2 className="h-4 w-4" />} onClick={() => approve.mutate()} loading={approve.isPending} disabled={busy || !minutesText}>Approve</Button>}
            {canSend && <Button icon={<Send className="h-4 w-4" />} onClick={() => send.mutate()} loading={send.isPending} disabled={busy}>{m?.status === 'Sent' ? 'Re-send' : 'Send to attendees'}</Button>}
          </div>
        </div>
      }>
      {isLoading || !details ? <div className="space-y-3">{[...Array(4)].map((_, i) => <Skeleton key={i} className="h-16" />)}</div> : (
        <div className="space-y-5">
          <div className="grid sm:grid-cols-2 gap-4">
            <Field label="Meeting title"><Input value={details.title} onChange={(e) => setDetails({ ...details, title: e.target.value })} /></Field>
            <Field label="Date & time"><Input type="datetime-local" value={details.meetingDate} onChange={(e) => setDetails({ ...details, meetingDate: e.target.value })} /></Field>
          </div>
          <div className="grid sm:grid-cols-2 gap-4">
            <Field label="Location"><Input value={details.location} onChange={(e) => setDetails({ ...details, location: e.target.value })} /></Field>
            <Field label="Attendees"><Textarea value={details.attendees} onChange={(e) => setDetails({ ...details, attendees: e.target.value })} className="min-h-[42px]" /></Field>
          </div>
          <Field label="Meeting notes" hint="Edit and regenerate any time to refine the minutes.">
            <Textarea value={details.rawNotes} onChange={(e) => setDetails({ ...details, rawNotes: e.target.value })} className="min-h-[110px]" />
          </Field>

          <div>
            <div className="flex items-center justify-between mb-1.5">
              <p className="text-sm font-semibold">Generated minutes {m?.status === 'Preview' && <span className="text-brand-500">— preview</span>}</p>
              <span className="text-[11px] text-slate-400 inline-flex items-center gap-1"><SpellCheck className="h-3.5 w-3.5" />Grammar corrected automatically</span>
            </div>
            {minutesText
              ? <Textarea value={minutesText} onChange={(e) => setMinutesText(e.target.value)} className="min-h-[220px] font-mono text-[13px] leading-relaxed" />
              : <div className="rounded-xl border border-dashed border-[var(--line)] p-6 text-center text-sm text-slate-400">
                  Click <span className="font-semibold">Generate minutes</span> to turn your notes into structured minutes, then review and edit here before sending.
                </div>}
            {hasPreview && <p className="text-xs text-slate-400 mt-2">Make any corrections above, then {canApprove ? 'Approve' : 'Save'} → Send. Editing approved minutes returns them to preview for re-approval.</p>}
          </div>
        </div>
      )}
    </Modal>
  )
}
