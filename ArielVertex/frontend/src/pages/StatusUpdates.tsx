import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { CalendarClock, Send, AlertTriangle, Layers } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import { useEnums } from '../lib/hooks'
import type { StatusUpdate } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, CardHeader, StatusPill, Avatar, Button, EmptyState, Badge } from '../ui/primitives'
import { Field, Input, Select, Textarea } from '../ui/form'
import { useToast } from '../ui/Toast'
import { asArray, fmtDate } from '../ui/util'
import { P } from '../components/nav'

interface MineResp { updates: StatusUpdate[]; assignableProjects: { projectId: number; name: string }[] }

export default function StatusUpdates() {
  const { has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const enums = useEnums()
  const canSubmit = has(P.StatusSubmit)

  const { data } = useQuery({ queryKey: ['status-mine'], queryFn: async () => (await api.get<MineResp>('/status-updates/mine')).data })
  const assignableProjects = asArray(data?.assignableProjects)
  const updates = asArray(data?.updates)
  const [form, setForm] = useState({ projectId: 0, updateDate: new Date().toISOString().slice(0, 10), workCompleted: '', nextPlannedWork: '', blockers: '', billableHours: 8, nonBillableHours: 0, status: 'OnTrack', clientShareableSummary: '', internalNote: '' })

  const submit = useMutation({
    mutationFn: () => api.post('/status-updates', form),
    onSuccess: () => { push('Status update submitted'); qc.invalidateQueries({ queryKey: ['status-mine'] }); setForm({ ...form, workCompleted: '', nextPlannedWork: '', blockers: '', clientShareableSummary: '', internalNote: '' }) },
    onError: (e) => push(apiError(e), 'error'),
  })

  return (
    <div>
      <PageHeader title="Status Updates" subtitle="Submit your daily project status and track your history" icon={<CalendarClock className="h-5 w-5" />}
        actions={has(P.StatusViewAll) ? <ConsolidatedButton /> : undefined} />

      <div className="grid lg:grid-cols-5 gap-4">
        {canSubmit && (
          <Card className="lg:col-span-2 h-fit">
            <CardHeader title="Submit update" subtitle="Only projects assigned to you" icon={<Send className="h-[18px] w-[18px]" />} />
            <div className="p-5 space-y-4">
              <Field label="Project" required>
                <Select value={form.projectId} onChange={(e) => setForm({ ...form, projectId: Number(e.target.value) })}>
                  <option value={0}>Select project…</option>
                  {assignableProjects.map((p) => <option key={p.projectId} value={p.projectId}>{p.name}</option>)}
                </Select>
              </Field>
              <div className="grid grid-cols-3 gap-3">
                <Field label="Date"><Input type="date" value={form.updateDate} onChange={(e) => setForm({ ...form, updateDate: e.target.value })} /></Field>
                <Field label="Billable hrs"><Input type="number" min={0} max={24} value={form.billableHours} onChange={(e) => setForm({ ...form, billableHours: Number(e.target.value) })} /></Field>
                <Field label="Non-billable hrs"><Input type="number" min={0} max={24} value={form.nonBillableHours} onChange={(e) => setForm({ ...form, nonBillableHours: Number(e.target.value) })} /></Field>
              </div>
              <Field label="Status"><Select value={form.status} onChange={(e) => setForm({ ...form, status: e.target.value })}>{enums.data?.updateStatuses.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select></Field>
              <Field label="Work completed" required><Textarea value={form.workCompleted} onChange={(e) => setForm({ ...form, workCompleted: e.target.value })} placeholder="What did you finish today?" /></Field>
              <Field label="Next planned work"><Textarea value={form.nextPlannedWork} onChange={(e) => setForm({ ...form, nextPlannedWork: e.target.value })} /></Field>
              <Field label="Blockers / dependencies"><Textarea value={form.blockers} onChange={(e) => setForm({ ...form, blockers: e.target.value })} placeholder="Anything blocking you?" /></Field>
              <Field label="Client-shareable summary" hint="This is the only part rolled into the business summary."><Textarea value={form.clientShareableSummary} onChange={(e) => setForm({ ...form, clientShareableSummary: e.target.value })} /></Field>
              <Button className="w-full" loading={submit.isPending} disabled={!form.projectId || !form.workCompleted} onClick={() => submit.mutate()} icon={<Send className="h-4 w-4" />}>Submit update</Button>
            </div>
          </Card>
        )}

        <Card className={canSubmit ? 'lg:col-span-3' : 'lg:col-span-5'}>
          <CardHeader title="My recent updates" subtitle="Your last 30 submissions" />
          <div className="p-5 space-y-3">
            {updates.length ? updates.map((s) => (
              <div key={s.id} className="rounded-xl border border-[var(--line)] p-4">
                <div className="flex items-center gap-2 mb-2">
                  <Badge t="brand">{s.projectName}</Badge>
                  <span className="text-xs text-slate-400">{fmtDate(s.updateDate)} · {s.hoursSpent}h ({s.billableHours}b / {s.nonBillableHours}nb)</span>
                  <div className="ml-auto"><StatusPill value={s.status} /></div>
                </div>
                <p className="text-sm">{s.workCompleted}</p>
                {s.blockers && <p className="text-sm mt-1 inline-flex items-center gap-1 text-rose-600 dark:text-rose-400"><AlertTriangle className="h-3.5 w-3.5" />{s.blockers}</p>}
              </div>
            )) : <EmptyState icon={<CalendarClock className="h-6 w-6" />} title="No updates yet" hint="Submit your first status update to get started." />}
          </div>
        </Card>
      </div>
    </div>
  )
}

function ConsolidatedButton() {
  const [pid, setPid] = useState<number | ''>('')
  const { data: projects } = useQuery({
    queryKey: ['projects', 'options', 'status-updates'],
    queryFn: async () => asArray((await api.get('/projects', { params: { pageSize: 50 } })).data?.items) as any[],
  })
  const projectOptions = asArray(projects)
  const { data: summary } = useQuery({
    queryKey: ['consolidated', pid], enabled: !!pid,
    queryFn: async () => (await api.get(`/status-updates/consolidated/${pid}`)).data as any,
  })
  return (
    <div className="flex items-center gap-2">
      <Select value={pid} onChange={(e) => setPid(e.target.value ? Number(e.target.value) : '')} className="w-52">
        <option value="">Consolidated summary…</option>
        {projectOptions.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
      </Select>
      {summary && (
        <Badge t={summary.missingUpdateFrom.length ? 'warn' : 'good'}>
          <Layers className="h-3.5 w-3.5" />{summary.membersReporting}/{summary.membersExpected} reporting
        </Badge>
      )}
    </div>
  )
}
