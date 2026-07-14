import { useEffect, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { SlidersHorizontal, Save, Mail, ToggleLeft, Pencil } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { PageHeader } from '../components/PageHeader'
import { Card, CardHeader, Button, Skeleton, Badge } from '../ui/primitives'
import { Field, Input, Textarea } from '../ui/form'
import { Modal } from '../ui/Modal'
import { useToast } from '../ui/Toast'

interface Setting { key: string; value: string; group: string; label: string; type: string; description: string; editable: boolean }
interface Template { key: string; name: string; subject: string; body: string; placeholders: string }

export default function Configuration() {
  const { push } = useToast()
  const qc = useQueryClient()
  const { data: settings, isLoading } = useQuery({ queryKey: ['admin-config'], queryFn: async () => (await api.get<Setting[]>('/admin/config')).data })
  const { data: templates } = useQuery({ queryKey: ['admin-templates'], queryFn: async () => (await api.get<Template[]>('/admin/templates')).data })

  const [dirty, setDirty] = useState<Record<string, string>>({})
  useEffect(() => { setDirty({}) }, [settings])
  const val = (s: Setting) => dirty[s.key] ?? s.value
  const set = (k: string, v: string) => setDirty((d) => ({ ...d, [k]: v }))

  const save = useMutation({
    mutationFn: () => api.put('/admin/config', { settings: Object.entries(dirty).map(([key, value]) => ({ key, value })) }),
    onSuccess: () => { push('Configuration saved'); qc.invalidateQueries({ queryKey: ['admin-config'] }); qc.invalidateQueries({ queryKey: ['features'] }); setDirty({}) },
    onError: (e) => push(apiError(e), 'error'),
  })

  const [editTpl, setEditTpl] = useState<Template | null>(null)
  const groups = [...new Set((settings ?? []).map((s) => s.group))]

  return (
    <div>
      <PageHeader title="Configuration" subtitle="Change values, toggle whole modules, and edit email templates — no code changes" icon={<SlidersHorizontal className="h-5 w-5" />}
        actions={Object.keys(dirty).length > 0 ? <Button icon={<Save className="h-4 w-4" />} loading={save.isPending} onClick={() => save.mutate()}>Save {Object.keys(dirty).length} change(s)</Button> : undefined} />

      {isLoading ? <div className="grid lg:grid-cols-2 gap-4">{[...Array(4)].map((_, i) => <Skeleton key={i} className="h-40" />)}</div> : (
        <div className="grid lg:grid-cols-2 gap-4">
          {groups.map((group) => (
            <Card key={group}>
              <CardHeader title={group} icon={group === 'Modules' ? <ToggleLeft className="h-[18px] w-[18px]" /> : <SlidersHorizontal className="h-[18px] w-[18px]" />} />
              <div className="p-5 space-y-4">
                {settings!.filter((s) => s.group === group).map((s) => (
                  <div key={s.key} className="flex items-start justify-between gap-4">
                    <div className="min-w-0">
                      <p className="text-sm font-semibold">{s.label}</p>
                      <p className="text-xs text-slate-400">{s.description}</p>
                    </div>
                    <div className="shrink-0">
                      {s.type === 'bool'
                        ? <button onClick={() => set(s.key, val(s) === 'true' ? 'false' : 'true')} className={`relative h-6 w-11 rounded-full transition ${val(s) === 'true' ? 'bg-brand-500' : 'bg-slate-300 dark:bg-navy-500'}`}><span className={`absolute top-0.5 h-5 w-5 rounded-full bg-white transition-all ${val(s) === 'true' ? 'left-[22px]' : 'left-0.5'}`} /></button>
                        : s.type === 'number'
                          ? <Input type="number" value={val(s)} onChange={(e) => set(s.key, e.target.value)} className="w-24 text-right" />
                          : <Input value={val(s)} onChange={(e) => set(s.key, e.target.value)} className="w-48" />}
                    </div>
                  </div>
                ))}
              </div>
            </Card>
          ))}

          <Card className="lg:col-span-2">
            <CardHeader title="Email / notification templates" subtitle="Edit the wording sent by the portal" icon={<Mail className="h-[18px] w-[18px]" />} />
            <div className="p-5 space-y-3">
              {templates?.length ? templates.map((t) => (
                <div key={t.key} className="flex items-start justify-between gap-4 rounded-xl border border-[var(--line)] p-4">
                  <div className="min-w-0">
                    <p className="font-semibold text-sm">{t.name}</p>
                    <p className="text-xs text-slate-500 truncate">{t.subject}</p>
                    <p className="text-[11px] text-slate-400 mt-1">Placeholders: {t.placeholders}</p>
                  </div>
                  <Button size="sm" variant="secondary" icon={<Pencil className="h-4 w-4" />} onClick={() => setEditTpl(t)}>Edit</Button>
                </div>
              )) : <p className="text-sm text-slate-400">No templates.</p>}
            </div>
          </Card>
        </div>
      )}

      {editTpl && <TemplateModal tpl={editTpl} onClose={() => setEditTpl(null)} onDone={() => { qc.invalidateQueries({ queryKey: ['admin-templates'] }); setEditTpl(null) }} push={push} />}
    </div>
  )
}

function TemplateModal({ tpl, onClose, onDone, push }: any) {
  const [form, setForm] = useState({ subject: tpl.subject, body: tpl.body })
  const save = useMutation({
    mutationFn: () => api.put(`/admin/templates/${tpl.key}`, form),
    onSuccess: () => { push('Template saved'); onDone() },
    onError: (e: any) => push(apiError(e), 'error'),
  })
  return (
    <Modal open onClose={onClose} size="lg" title={`Edit template — ${tpl.name}`} subtitle={`Placeholders: ${tpl.placeholders}`}
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={save.isPending} onClick={() => save.mutate()}>Save</Button></>}>
      <div className="space-y-4">
        <Field label="Subject"><Input value={form.subject} onChange={(e) => setForm({ ...form, subject: e.target.value })} /></Field>
        <Field label="Body"><Textarea value={form.body} onChange={(e) => setForm({ ...form, body: e.target.value })} className="min-h-[160px]" /></Field>
      </div>
    </Modal>
  )
}
