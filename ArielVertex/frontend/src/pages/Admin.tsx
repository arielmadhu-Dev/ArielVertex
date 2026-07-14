import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ShieldCheck, RefreshCw, ScrollText, Plug, CheckCircle2, XCircle } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import type { Paged } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, CardHeader, Badge, Button, EmptyState, StatusPill } from '../ui/primitives'
import { useToast } from '../ui/Toast'
import { fmtDateTime } from '../ui/util'
import { P } from '../components/nav'

// (types not exported in types.ts — declare inline)
interface AuditRow { id: number; actorName: string; actionLabel: string; entityType: string; summary: string; createdAt: string }
interface SyncRow { id: number; runAt: string; status: string; created: number; updated: number; failed: number; wasManual: boolean; triggeredBy: string; message?: string }
interface Integration { authMode: string; microsoftLoginEnabled: boolean; graphMeetingsLive: boolean; directorySyncLive: boolean; outlookNotificationsLive: boolean; allowedDomain: string }

export default function Admin() {
  const { has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()

  const integration = useQuery({ queryKey: ['integration'], queryFn: async () => (await api.get<Integration>('/admin/integration-status')).data })
  const audit = useQuery({ queryKey: ['audit'], enabled: has(P.AuditView), queryFn: async () => (await api.get<Paged<AuditRow>>('/admin/audit', { params: { pageSize: 25 } })).data })
  const syncLogs = useQuery({ queryKey: ['sync-logs'], enabled: has(P.SyncRun), queryFn: async () => (await api.get<SyncRow[]>('/admin/sync-logs')).data })

  const runSync = useMutation({
    mutationFn: () => api.post('/admin/sync/run'),
    onSuccess: (r: any) => { push(r.data.message || 'Sync run recorded'); qc.invalidateQueries({ queryKey: ['sync-logs'] }) },
    onError: (e) => push(apiError(e), 'error'),
  })

  const Flag = ({ on, label }: { on: boolean; label: string }) => (
    <div className="flex items-center justify-between rounded-xl border border-[var(--line)] px-3.5 py-2.5">
      <span className="text-sm font-medium">{label}</span>
      {on ? <Badge t="good"><CheckCircle2 className="h-3.5 w-3.5" />Live</Badge> : <Badge t="neutral"><XCircle className="h-3.5 w-3.5" />Stubbed</Badge>}
    </div>
  )

  return (
    <div>
      <PageHeader title="Admin & Audit" subtitle="Integration status, employee sync, and the security audit trail" icon={<ShieldCheck className="h-5 w-5" />} />

      <div className="grid lg:grid-cols-3 gap-4">
        <Card>
          <CardHeader title="Microsoft 365 Integration" subtitle="Boundaries flip to live with the Entra app registration" icon={<Plug className="h-[18px] w-[18px]" />} />
          <div className="p-5 space-y-2.5">
            <div className="flex items-center justify-between rounded-xl border border-[var(--line)] px-3.5 py-2.5">
              <span className="text-sm font-medium">Auth mode</span><Badge t="brand">{integration.data?.authMode ?? '—'}</Badge>
            </div>
            <Flag on={!!integration.data?.microsoftLoginEnabled} label="Microsoft (Entra) login" />
            <Flag on={!!integration.data?.graphMeetingsLive} label="Teams / Outlook meetings" />
            <Flag on={!!integration.data?.directorySyncLive} label="Entra directory sync" />
            <Flag on={!!integration.data?.outlookNotificationsLive} label="Outlook email delivery" />
            <div className="flex items-center justify-between rounded-xl border border-[var(--line)] px-3.5 py-2.5">
              <span className="text-sm font-medium">Allowed domain</span><span className="text-sm font-semibold">{integration.data?.allowedDomain}</span>
            </div>
          </div>
        </Card>

        {has(P.SyncRun) && (
          <Card className="lg:col-span-2">
            <CardHeader title="Employee Sync" subtitle="Run history with created / updated / failed counts" icon={<RefreshCw className="h-[18px] w-[18px]" />}
              action={<Button size="sm" loading={runSync.isPending} icon={<RefreshCw className="h-4 w-4" />} onClick={() => runSync.mutate()}>Run sync</Button>} />
            <div className="p-5 space-y-2">
              {syncLogs.data?.length ? syncLogs.data.map((s) => (
                <div key={s.id} className="flex items-center gap-3 rounded-xl border border-[var(--line)] px-3.5 py-2.5">
                  <StatusPill value={s.status} />
                  <div className="flex-1 min-w-0"><p className="text-sm truncate">{s.message || 'Sync run'}</p><p className="text-xs text-slate-400">{fmtDateTime(s.runAt)} · {s.wasManual ? 'manual' : 'scheduled'} · by {s.triggeredBy}</p></div>
                  <div className="text-xs text-slate-500 whitespace-nowrap">+{s.created} / ~{s.updated} / !{s.failed}</div>
                </div>
              )) : <EmptyState icon={<RefreshCw className="h-6 w-6" />} title="No sync runs yet" />}
            </div>
          </Card>
        )}
      </div>

      {has(P.AuditView) && (
        <Card className="mt-4 overflow-hidden">
          <CardHeader title="Audit Trail" subtitle="Every sensitive action is recorded" icon={<ScrollText className="h-[18px] w-[18px]" />} />
          <div className="overflow-x-auto av-scroll-x">
            <table className="w-full text-sm">
              <thead><tr className="text-left text-xs uppercase tracking-wide text-slate-400 border-b border-[var(--line)]">
                <th className="px-5 py-3">When</th><th className="px-5 py-3">Actor</th><th className="px-5 py-3">Action</th><th className="px-5 py-3">Summary</th>
              </tr></thead>
              <tbody>
                {audit.data?.items.map((a) => (
                  <tr key={a.id} className="border-b border-[var(--line)] last:border-0">
                    <td className="px-5 py-2.5 text-slate-400 whitespace-nowrap">{fmtDateTime(a.createdAt)}</td>
                    <td className="px-5 py-2.5 font-medium">{a.actorName}</td>
                    <td className="px-5 py-2.5"><Badge t="info">{a.actionLabel}</Badge></td>
                    <td className="px-5 py-2.5 text-slate-600 dark:text-slate-300">{a.summary}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {!audit.data?.items.length && <EmptyState icon={<ScrollText className="h-6 w-6" />} title="No audit entries yet" />}
        </Card>
      )}
    </div>
  )
}
