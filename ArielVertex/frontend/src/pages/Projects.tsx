import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { FolderKanban, Plus, Search, Users, CalendarDays } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import { useEnums } from '../lib/hooks'
import type { Paged, ProjectListItem } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, StatusPill, Badge, EmptyState, Skeleton, Button } from '../ui/primitives'
import { Modal } from '../ui/Modal'
import { Field, Input, Select, Textarea } from '../ui/form'
import { useToast } from '../ui/Toast'
import { fmtDate } from '../ui/util'
import { P } from '../components/nav'

export default function Projects() {
  const { has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const [search, setSearch] = useState('')
  const [open, setOpen] = useState(false)
  const enums = useEnums()

  const { data, isLoading } = useQuery({
    queryKey: ['projects', search],
    queryFn: async () => (await api.get<Paged<ProjectListItem>>('/projects', { params: { search, pageSize: 50 } })).data,
  })

  const [form, setForm] = useState({ code: '', name: '', clientName: '', description: '', status: 'Active', priority: 'Medium', startDate: new Date().toISOString().slice(0, 10) })
  const create = useMutation({
    mutationFn: () => api.post('/projects', form),
    onSuccess: () => { push('Project created'); setOpen(false); qc.invalidateQueries({ queryKey: ['projects'] }); setForm({ ...form, code: '', name: '', clientName: '', description: '' }) },
    onError: (e) => push(apiError(e), 'error'),
  })

  return (
    <div>
      <PageHeader title="Projects" subtitle="Every project you can access, scoped to your role" icon={<FolderKanban className="h-5 w-5" />}
        actions={has(P.ProjectsCreate) ? (
          <Button icon={<Plus className="h-4 w-4" />} onClick={() => setOpen(true)}>New Project</Button>
        ) : undefined} />

      <div className="relative mb-4 max-w-sm">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
        <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search projects…" className="pl-9" />
      </div>

      {isLoading ? (
        <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-4">{[...Array(6)].map((_, i) => <Skeleton key={i} className="h-40" />)}</div>
      ) : data?.items.length ? (
        <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {data.items.map((p, i) => (
            <motion.div key={p.id} initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: i * 0.04 }}>
              <Link to={`/projects/${p.id}`}>
                <Card className="p-5 h-full hover:shadow-pop hover:border-brand-200 transition group">
                  <div className="flex items-start justify-between">
                    <div className="grid place-items-center h-11 w-11 rounded-2xl bg-brand-50 dark:bg-brand-900/40 text-brand-600 font-extrabold text-sm">{p.code}</div>
                    <StatusPill value={p.health} />
                  </div>
                  <h3 className="mt-3 font-bold text-[15px] group-hover:text-brand-600 transition">{p.name}</h3>
                  <p className="text-xs text-slate-500">{p.clientName || 'Internal'}</p>
                  <div className="mt-3 flex flex-wrap gap-1.5">
                    <StatusPill value={p.status} />
                    <Badge t="info">{p.priority}</Badge>
                    {p.myRoleOnProject && <Badge t="brand">{p.myRoleOnProject.replace(/([a-z])([A-Z])/g, '$1 $2')}</Badge>}
                  </div>
                  <div className="mt-4 pt-3 border-t border-[var(--line)] flex items-center justify-between text-xs text-slate-500">
                    <span className="inline-flex items-center gap-1"><Users className="h-3.5 w-3.5" />{p.memberCount} members</span>
                    <span className="inline-flex items-center gap-1"><CalendarDays className="h-3.5 w-3.5" />{fmtDate(p.startDate)}</span>
                  </div>
                </Card>
              </Link>
            </motion.div>
          ))}
        </div>
      ) : (
        <Card><EmptyState icon={<FolderKanban className="h-6 w-6" />} title="No projects yet" hint="Projects assigned to you will appear here." /></Card>
      )}

      <Modal open={open} onClose={() => setOpen(false)} title="Create project" subtitle="Set up a new project workspace"
        footer={<><Button variant="secondary" onClick={() => setOpen(false)}>Cancel</Button><Button loading={create.isPending} onClick={() => create.mutate()}>Create project</Button></>}>
        <div className="grid sm:grid-cols-2 gap-4">
          <Field label="Code" required><Input value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value.toUpperCase() })} placeholder="MIB" /></Field>
          <Field label="Client"><Input value={form.clientName} onChange={(e) => setForm({ ...form, clientName: e.target.value })} placeholder="Client name" /></Field>
          <Field label="Name" required><Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="Project name" /></Field>
          <Field label="Start date"><Input type="date" value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} /></Field>
          <Field label="Status"><Select value={form.status} onChange={(e) => setForm({ ...form, status: e.target.value })}>{enums.data?.projectStatus.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select></Field>
          <Field label="Priority"><Select value={form.priority} onChange={(e) => setForm({ ...form, priority: e.target.value })}>{enums.data?.priorities.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select></Field>
          <div className="sm:col-span-2"><Field label="Description"><Textarea value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} placeholder="What is this project about?" /></Field></div>
        </div>
      </Modal>
    </div>
  )
}
