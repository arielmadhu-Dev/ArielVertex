import { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import {
  ArrowLeft, Users, FileText, Video, CalendarPlus, CalendarClock, Plus, ExternalLink,
  UserPlus, Trash2, ClipboardCheck, Building2, Info, Download, UploadCloud,
} from 'lucide-react'
import { useAuth } from '../lib/auth'
import { api, apiError } from '../lib/api'
import { useEnums, useEmployees } from '../lib/hooks'
import type { ProjectDetail, StatusUpdate } from '../lib/types'
import { Card, CardHeader, StatusPill, Badge, Avatar, Button, EmptyState, Skeleton } from '../ui/primitives'
import { Tabs } from '../ui/widgets'
import { Modal } from '../ui/Modal'
import { Field, Input, Select, Textarea } from '../ui/form'
import { useToast } from '../ui/Toast'
import { asArray, asText, fmtDate, fmtDateTime, cx, nice } from '../ui/util'


export default function ProjectWorkspace() {
  const { id } = useParams()
  const pid = Number(id)
  const [tab, setTab] = useState('overview')
  const { data: p, isLoading } = useQuery({ queryKey: ['project', pid], queryFn: async () => (await api.get<ProjectDetail>(`/projects/${pid}`)).data })

  if (isLoading) return <div className="space-y-4"><Skeleton className="h-24" /><Skeleton className="h-64" /></div>
  if (!p) return <EmptyState icon={<Info className="h-6 w-6" />} title="Project not found" />

  const members = asArray(p.members).filter((member) => member.isActive)
  const tags = asArray(p.tags)

  const tabs = [
    { key: 'overview', label: 'Overview' },
    { key: 'team', label: 'Team', badge: members.length },
    { key: 'documents', label: 'Documents' },
    { key: 'calls', label: 'Customer Calls' },
    { key: 'status', label: 'Status Updates' },
    { key: 'reviews', label: 'Reviews' },
    { key: 'comments', label: 'Business Comments' },
  ]

  return (
    <div>
      <Link to="/projects" className="inline-flex items-center gap-1.5 text-sm font-semibold text-slate-500 hover:text-brand-600 mb-4"><ArrowLeft className="h-4 w-4" /> Projects</Link>

      <Card className="p-6 mb-4 overflow-hidden relative">
        <div className="absolute right-0 top-0 h-32 w-32 av-brand-gradient opacity-10 blur-3xl rounded-full" />
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="flex items-start gap-4">
            <div className="grid place-items-center h-14 w-14 rounded-2xl av-brand-gradient text-white font-extrabold">{asText(p.code, 'PRJ')}</div>
            <div>
              <h1 className="text-2xl font-extrabold tracking-tight">{asText(p.name, 'Untitled project')}</h1>
              <p className="text-sm text-slate-500">{p.clientName || 'Internal'} · Owner: {p.businessOwner || '—'}</p>
              <div className="mt-2 flex flex-wrap gap-1.5">
                <StatusPill value={p.status} /><StatusPill value={p.health} /><Badge t="info">{p.priority} priority</Badge>
                {p.myRoleOnProject && <Badge t="brand">You: {nice(p.myRoleOnProject)}</Badge>}
              </div>
            </div>
          </div>
          <div className="text-sm text-right text-slate-500">
            <p><span className="font-semibold text-[var(--ink)]">Start:</span> {fmtDate(p.startDate)}</p>
            <p><span className="font-semibold text-[var(--ink)]">Target:</span> {fmtDate(p.expectedEndDate)}</p>
          </div>
        </div>
        {tags.length > 0 && <div className="mt-4 flex flex-wrap gap-1.5">{tags.map((t) => <span key={t} className="text-xs rounded-md bg-slate-100 dark:bg-navy-600 px-2 py-0.5 text-slate-500">#{t}</span>)}</div>}
      </Card>

      <Card className="overflow-hidden">
        <div className="px-4 pt-2"><Tabs tabs={tabs} active={tab} onChange={setTab} /></div>
        <motion.div key={tab} initial={{ opacity: 0, y: 6 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.2 }} className="p-5">
          {tab === 'overview' && <Overview p={p} />}
          {tab === 'team' && <Team p={p} />}
          {tab === 'documents' && <Documents pid={pid} canManage={p.canManage} />}
          {tab === 'calls' && <Calls pid={pid} canManage={p.canManage} />}
          {tab === 'status' && <StatusTab pid={pid} />}
          {tab === 'reviews' && <ReviewsTab />}
          {tab === 'comments' && <Comments pid={pid} />}
        </motion.div>
      </Card>
    </div>
  )
}

function Overview({ p }: { p: ProjectDetail }) {
  return (
    <div className="grid lg:grid-cols-3 gap-5">
      <div className="lg:col-span-2 space-y-4">
        <div>
          <p className="av-label mb-1">Description</p>
          <p className="text-sm leading-relaxed">{p.description || 'No description provided.'}</p>
        </div>
        <div>
          <p className="av-label mb-1">Notes</p>
          <p className="text-sm leading-relaxed text-slate-600 dark:text-slate-300">{p.notes || '—'}</p>
        </div>
      </div>
      <div className="space-y-3">
        {[['Status', p.status], ['Health', p.health], ['Priority', p.priority], ['Client', p.clientName || '—'], ['Business Owner', p.businessOwner || '—']].map(([k, v]) => (
          <div key={k} className="flex items-center justify-between rounded-xl border border-[var(--line)] px-3.5 py-2.5">
            <span className="text-xs font-semibold uppercase tracking-wide text-slate-400">{k}</span>
            <span className="text-sm font-semibold">{v as string}</span>
          </div>
        ))}
      </div>
    </div>
  )
}

function Team({ p }: { p: ProjectDetail }) {
  const qc = useQueryClient()
  const { push } = useToast()
  const employees = useEmployees()
  const enums = useEnums()
  const [open, setOpen] = useState(false)
  const [form, setForm] = useState({ userId: 0, roleOnProject: 'Developer', allocationPct: 100, startDate: new Date().toISOString().slice(0, 10) })

  const add = useMutation({
    mutationFn: () => api.post(`/projects/${p.id}/members`, form),
    onSuccess: () => { push('Team member added'); setOpen(false); qc.invalidateQueries({ queryKey: ['project', p.id] }) },
    onError: (e) => push(apiError(e), 'error'),
  })
  const remove = useMutation({
    mutationFn: (memberId: number) => api.delete(`/projects/${p.id}/members/${memberId}`),
    onSuccess: () => { push('Member removed'); qc.invalidateQueries({ queryKey: ['project', p.id] }) },
    onError: (e) => push(apiError(e), 'error'),
  })
  const members = asArray(p.members).filter((member) => member.isActive)

  return (
    <div>
      <div className="flex justify-between items-center mb-4">
        <p className="text-sm text-slate-500">{members.length} people on this project</p>
        {p.canManage && <Button size="sm" icon={<UserPlus className="h-4 w-4" />} onClick={() => setOpen(true)}>Add member</Button>}
      </div>
      <div className="grid sm:grid-cols-2 gap-3">
        {members.map((m) => (
          <div key={m.id} className="flex items-center gap-3 rounded-xl border border-[var(--line)] p-3">
            <Avatar name={m.name} color={m.avatarColor} size={42} />
            <div className="min-w-0 flex-1">
              <p className="font-semibold text-sm truncate">{m.name}</p>
              <p className="text-xs text-slate-500 truncate">{m.designation}</p>
              <div className="mt-1 flex items-center gap-1.5">
                <Badge t="brand">{nice(m.roleOnProject)}</Badge>
                <span className="text-[11px] text-slate-400">{m.allocationPct}% allocated</span>
              </div>
            </div>
            {p.canManage && <button onClick={() => remove.mutate(m.id)} className="text-slate-300 hover:text-rose-500 p-1"><Trash2 className="h-4 w-4" /></button>}
          </div>
        ))}
      </div>

      <Modal open={open} onClose={() => setOpen(false)} title="Add team member"
        footer={<><Button variant="secondary" onClick={() => setOpen(false)}>Cancel</Button><Button loading={add.isPending} disabled={!form.userId} onClick={() => add.mutate()}>Add to project</Button></>}>
        <div className="grid sm:grid-cols-2 gap-4">
          <div className="sm:col-span-2"><Field label="Employee" required>
            <Select value={form.userId} onChange={(e) => setForm({ ...form, userId: Number(e.target.value) })}>
              <option value={0}>Select an employee…</option>
              {asArray(employees.data).filter((e) => !members.some((m) => m.userId === e.id)).map((e) => <option key={e.id} value={e.id}>{e.name} - {e.designation}</option>)}
            </Select>
          </Field></div>
          <Field label="Role on project"><Select value={form.roleOnProject} onChange={(e) => setForm({ ...form, roleOnProject: e.target.value })}>{enums.data?.projectRoles.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select></Field>
          <Field label="Allocation %"><Input type="number" min={0} max={100} value={form.allocationPct} onChange={(e) => setForm({ ...form, allocationPct: Number(e.target.value) })} /></Field>
        </div>
      </Modal>
    </div>
  )
}

function fmtBytes(n: number) {
  if (!n) return ''
  const u = ['B', 'KB', 'MB', 'GB']; let i = 0; let v = n
  while (v >= 1024 && i < u.length - 1) { v /= 1024; i++ }
  return `${v.toFixed(v < 10 && i > 0 ? 1 : 0)} ${u[i]}`
}

function Documents({ pid, canManage }: { pid: number; canManage: boolean }) {
  const qc = useQueryClient()
  const { push } = useToast()
  const { has } = useAuth()
  const enums = useEnums()
  const [open, setOpen] = useState(false)
  const { data } = useQuery({ queryKey: ['docs', pid], queryFn: async () => (await api.get<any[]>(`/projects/${pid}/documents`)).data })

  const [meta, setMeta] = useState({ title: '', description: '', category: 'Requirement', visibility: 'Internal', isVideoLink: false, videoUrl: '' })
  const [file, setFile] = useState<File | null>(null)

  const add = useMutation({
    mutationFn: async () => {
      if (meta.isVideoLink) {
        return api.post(`/projects/${pid}/documents`, { ...meta, fileName: null })
      }
      const fd = new FormData()
      fd.append('file', file!)
      fd.append('title', meta.title)
      fd.append('description', meta.description)
      fd.append('category', meta.category)
      fd.append('visibility', meta.visibility)
      return api.post(`/projects/${pid}/documents/upload`, fd)
    },
    onSuccess: () => { push(meta.isVideoLink ? 'Video link added' : 'File uploaded'); setOpen(false); setFile(null); setMeta({ ...meta, title: '', description: '', videoUrl: '' }); qc.invalidateQueries({ queryKey: ['docs', pid] }) },
    onError: (e) => push(apiError(e), 'error'),
  })

  const remove = useMutation({
    mutationFn: (docId: number) => api.delete(`/projects/${pid}/documents/${docId}`),
    onSuccess: () => { push('Document removed'); qc.invalidateQueries({ queryKey: ['docs', pid] }) },
    onError: (e) => push(apiError(e), 'error'),
  })

  const download = async (d: any) => {
    try {
      const res = await api.get(`/projects/${pid}/documents/${d.id}/download`, { responseType: 'blob' })
      const url = URL.createObjectURL(res.data)
      const a = document.createElement('a'); a.href = url; a.download = d.fileName || d.title; a.click()
      URL.revokeObjectURL(url)
    } catch (e) { push(apiError(e, 'Could not download the file.'), 'error') }
  }

  const canDelete = canManage && has('documents.delete')
  const ready = meta.title.trim() && (meta.isVideoLink ? meta.videoUrl.trim() : !!file)

  return (
    <div>
      <div className="flex justify-between items-center mb-4">
        <p className="text-sm text-slate-500">Requirement, technical, demo and meeting artifacts</p>
        {canManage && <Button size="sm" icon={<Plus className="h-4 w-4" />} onClick={() => setOpen(true)}>Add document</Button>}
      </div>
      {data?.length ? (
        <div className="grid sm:grid-cols-2 gap-3">
          {data.map((d) => (
            <div key={d.id} className="flex items-start gap-3 rounded-xl border border-[var(--line)] p-3.5">
              <div className="grid place-items-center h-10 w-10 rounded-xl bg-brand-50 dark:bg-brand-900/40 text-brand-500">{d.isVideoLink ? <Video className="h-5 w-5" /> : <FileText className="h-5 w-5" />}</div>
              <div className="min-w-0 flex-1">
                <p className="font-semibold text-sm truncate">{d.title}</p>
                <p className="text-xs text-slate-500 line-clamp-1">{d.fileName ? `${d.fileName} · ${fmtBytes(d.sizeBytes)}` : (d.description || nice(d.category))}</p>
                <div className="mt-1.5 flex items-center gap-1.5"><Badge t="info">{nice(d.category)}</Badge><Badge t="neutral">{nice(d.visibility)}</Badge>{d.uploadedBy && <span className="text-[11px] text-slate-400">by {d.uploadedBy}</span>}</div>
              </div>
              <div className="flex items-center gap-1 shrink-0">
                {d.isVideoLink
                  ? (d.videoUrl && <a href={d.videoUrl} target="_blank" rel="noreferrer" className="grid place-items-center h-8 w-8 rounded-lg text-brand-500 hover:bg-brand-50 dark:hover:bg-brand-900/40"><ExternalLink className="h-4 w-4" /></a>)
                  : <button onClick={() => download(d)} title="Download" className="grid place-items-center h-8 w-8 rounded-lg text-brand-500 hover:bg-brand-50 dark:hover:bg-brand-900/40"><Download className="h-4 w-4" /></button>}
                {canDelete && <button onClick={() => remove.mutate(d.id)} title="Remove" className="grid place-items-center h-8 w-8 rounded-lg text-slate-300 hover:text-rose-500 hover:bg-rose-50 dark:hover:bg-rose-900/30"><Trash2 className="h-4 w-4" /></button>}
              </div>
            </div>
          ))}
        </div>
      ) : <EmptyState icon={<FileText className="h-6 w-6" />} title="No documents yet" hint="Upload requirements, demos and meeting notes here." />}

      <Modal open={open} onClose={() => setOpen(false)} title="Add document" subtitle="Upload a file to private storage, or add a video link"
        footer={<><Button variant="secondary" onClick={() => setOpen(false)}>Cancel</Button><Button loading={add.isPending} disabled={!ready} onClick={() => add.mutate()}>{meta.isVideoLink ? 'Add link' : 'Upload'}</Button></>}>
        <div className="grid sm:grid-cols-2 gap-4">
          <div className="sm:col-span-2"><Field label="Title" required><Input value={meta.title} onChange={(e) => setMeta({ ...meta, title: e.target.value })} /></Field></div>
          <Field label="Category"><Select value={meta.category} onChange={(e) => setMeta({ ...meta, category: e.target.value })}>{enums.data?.documentCategories.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select></Field>
          <Field label="Visibility"><Select value={meta.visibility} onChange={(e) => setMeta({ ...meta, visibility: e.target.value })}>{enums.data?.visibilities.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select></Field>
          <div className="sm:col-span-2"><label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={meta.isVideoLink} onChange={(e) => setMeta({ ...meta, isVideoLink: e.target.checked })} /> This is a video link</label></div>
          {meta.isVideoLink
            ? <div className="sm:col-span-2"><Field label="Video URL"><Input value={meta.videoUrl} onChange={(e) => setMeta({ ...meta, videoUrl: e.target.value })} placeholder="https://…" /></Field></div>
            : <div className="sm:col-span-2"><Field label="File" hint="PDF, Office, images, text or zip · up to 25 MB · stored privately">
                <label className="flex flex-col items-center justify-center gap-2 rounded-xl border-2 border-dashed border-[var(--line)] p-6 cursor-pointer hover:border-brand-300 transition">
                  <UploadCloud className="h-7 w-7 text-brand-400" />
                  {file ? <span className="text-sm font-semibold">{file.name} · {fmtBytes(file.size)}</span> : <span className="text-sm text-slate-500">Click to choose a file</span>}
                  <input type="file" className="hidden" onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
                </label>
              </Field></div>}
          <div className="sm:col-span-2"><Field label="Description"><Textarea value={meta.description} onChange={(e) => setMeta({ ...meta, description: e.target.value })} /></Field></div>
        </div>
      </Modal>
    </div>
  )
}

function Calls({ pid, canManage }: { pid: number; canManage: boolean }) {
  const qc = useQueryClient()
  const { push } = useToast()
  const [open, setOpen] = useState(false)
  const { data } = useQuery({ queryKey: ['calls', pid], queryFn: async () => (await api.get<any[]>(`/projects/${pid}/calls`)).data })
  const [form, setForm] = useState({ title: '', agenda: '', type: 'Customer', scheduledAt: '', durationMinutes: 30, attendees: '' })

  const add = useMutation({
    mutationFn: () => api.post(`/projects/${pid}/calls`, form),
    onSuccess: (r) => { push(r.data.graphLive ? 'Call scheduled in Outlook/Teams' : 'Call scheduled (Teams link generated)'); setOpen(false); qc.invalidateQueries({ queryKey: ['calls', pid] }) },
    onError: (e) => push(apiError(e), 'error'),
  })

  return (
    <div>
      <div className="flex justify-between items-center mb-4">
        <p className="text-sm text-slate-500">Customer &amp; internal calls with Teams links</p>
        {canManage && <Button size="sm" icon={<CalendarPlus className="h-4 w-4" />} onClick={() => setOpen(true)}>Schedule call</Button>}
      </div>
      {data?.length ? (
        <div className="space-y-3">
          {data.map((c) => (
            <div key={c.id} className="flex items-center gap-4 rounded-xl border border-[var(--line)] p-4">
              <div className="grid place-items-center h-11 w-11 rounded-xl bg-brand-50 dark:bg-brand-900/40 text-brand-500"><CalendarClock className="h-5 w-5" /></div>
              <div className="min-w-0 flex-1">
                <p className="font-semibold text-sm">{c.title}</p>
                <p className="text-xs text-slate-500">{fmtDateTime(c.scheduledAt)} · {c.durationMinutes} min · {c.type}</p>
              </div>
              {c.teamsJoinUrl && <a href={c.teamsJoinUrl} target="_blank" rel="noreferrer"><Button size="sm" variant="subtle" icon={<Video className="h-4 w-4" />}>Join</Button></a>}
            </div>
          ))}
        </div>
      ) : <EmptyState icon={<CalendarClock className="h-6 w-6" />} title="No calls scheduled" hint="Schedule a customer call to generate a Teams meeting." />}

      <Modal open={open} onClose={() => setOpen(false)} title="Schedule call" subtitle="Creates an Outlook invite + Teams link"
        footer={<><Button variant="secondary" onClick={() => setOpen(false)}>Cancel</Button><Button loading={add.isPending} disabled={!form.title || !form.scheduledAt} onClick={() => add.mutate()}>Schedule</Button></>}>
        <div className="grid sm:grid-cols-2 gap-4">
          <div className="sm:col-span-2"><Field label="Title" required><Input value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} placeholder="MIB weekly sync" /></Field></div>
          <Field label="Date & time" required><Input type="datetime-local" value={form.scheduledAt} onChange={(e) => setForm({ ...form, scheduledAt: e.target.value })} /></Field>
          <Field label="Duration (min)"><Input type="number" value={form.durationMinutes} onChange={(e) => setForm({ ...form, durationMinutes: Number(e.target.value) })} /></Field>
          <div className="sm:col-span-2"><Field label="Attendees" hint="Comma-separated emails"><Input value={form.attendees} onChange={(e) => setForm({ ...form, attendees: e.target.value })} placeholder="a@x.com, b@x.com" /></Field></div>
          <div className="sm:col-span-2"><Field label="Agenda"><Textarea value={form.agenda} onChange={(e) => setForm({ ...form, agenda: e.target.value })} /></Field></div>
        </div>
      </Modal>
    </div>
  )
}

function StatusTab({ pid }: { pid: number }) {
  const { data } = useQuery({ queryKey: ['project-status', pid], queryFn: async () => (await api.get<StatusUpdate[]>(`/projects/${pid}/status-updates`)).data })
  if (!data?.length) return <EmptyState icon={<ClipboardCheck className="h-6 w-6" />} title="No status updates yet" hint="Developer updates for this project will show here." />
  return (
    <div className="space-y-3">
      {data.map((s) => (
        <div key={s.id} className="rounded-xl border border-[var(--line)] p-4">
          <div className="flex items-center gap-3 mb-2">
            <Avatar name={s.userName} color={s.avatarColor} size={34} />
            <div className="flex-1"><p className="font-semibold text-sm">{s.userName}</p><p className="text-xs text-slate-400">{fmtDate(s.updateDate)} · {s.hoursSpent}h</p></div>
            <StatusPill value={s.status} />
          </div>
          <p className="text-sm"><span className="font-semibold">Done:</span> {s.workCompleted}</p>
          {s.nextPlannedWork && <p className="text-sm mt-1"><span className="font-semibold">Next:</span> {s.nextPlannedWork}</p>}
          {s.blockers && <p className="text-sm mt-1 text-rose-600 dark:text-rose-400"><span className="font-semibold">Blockers:</span> {s.blockers}</p>}
        </div>
      ))}
    </div>
  )
}

function ReviewsTab() {
  return <EmptyState icon={<Building2 className="h-6 w-6" />} title="Reviews live in the Reviews module" hint="Open the Reviews section to request, schedule and submit reviews for this project."
    action={<Link to="/reviews"><Button size="sm">Go to Reviews</Button></Link>} />
}

function Comments({ pid }: { pid: number }) {
  const qc = useQueryClient()
  const { push } = useToast()
  const { user } = useAuth()
  const [text, setText] = useState('')
  const { data } = useQuery({ queryKey: ['comments', pid], queryFn: async () => (await api.get<any[]>(`/projects/${pid}/comments`)).data })

  const post = useMutation({
    mutationFn: () => api.post(`/projects/${pid}/comments`, { message: text }),
    onSuccess: () => { setText(''); qc.invalidateQueries({ queryKey: ['comments', pid] }) },
    onError: (e) => push(apiError(e), 'error'),
  })

  return (
    <div className="max-w-3xl">
      <p className="text-sm text-slate-500 mb-4">Shared thread between the delivery team and business stakeholders.</p>
      <div className="space-y-3 mb-5">
        {data?.length ? data.map((c) => {
          const mine = c.authorId === user?.id
          return (
            <div key={c.id} className={cx('flex gap-3', mine && 'flex-row-reverse')}>
              <Avatar name={c.authorName} color={c.avatarColor} size={36} />
              <div className={cx('rounded-2xl px-4 py-2.5 max-w-[80%]', mine ? 'bg-brand-500 text-white' : 'bg-slate-100 dark:bg-navy-600')}>
                <div className="flex items-center gap-2 mb-0.5">
                  <span className={cx('text-xs font-bold', mine && 'text-white')}>{c.authorName}</span>
                  <span className={cx('text-[10px]', mine ? 'text-brand-100' : 'text-slate-400')}>{nice(c.role || '')}</span>
                </div>
                <p className="text-sm leading-relaxed">{c.message}</p>
                <p className={cx('text-[10px] mt-1', mine ? 'text-brand-100' : 'text-slate-400')}>{fmtDateTime(c.createdAt)}</p>
              </div>
            </div>
          )
        }) : <EmptyState icon={<Building2 className="h-6 w-6" />} title="No comments yet" hint="Start the conversation below." />}
      </div>
      <div className="flex gap-2">
        <input value={text} onChange={(e) => setText(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && text.trim() && post.mutate()}
          placeholder="Write a comment…" className="av-input flex-1" />
        <Button icon={<Building2 className="h-4 w-4" />} loading={post.isPending} disabled={!text.trim()} onClick={() => post.mutate()}>Post</Button>
      </div>
    </div>
  )
}
