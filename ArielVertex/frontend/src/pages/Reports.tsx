import { useQuery } from '@tanstack/react-query'
import { BarChart, Bar, XAxis, YAxis, ResponsiveContainer, Tooltip, CartesianGrid } from 'recharts'
import { FileBarChart, Download, FolderKanban, Printer } from 'lucide-react'
import { api } from '../lib/api'
import { PageHeader } from '../components/PageHeader'
import { Card, CardHeader, Button, EmptyState, StatusPill } from '../ui/primitives'
import { useToast } from '../ui/Toast'

export default function Reports() {
  const { push } = useToast()
  const projects = useQuery({ queryKey: ['projects', ''], queryFn: async () => (await api.get('/projects', { params: { pageSize: 50 } })).data.items as any[] })
  const occupancy = useQuery({ queryKey: ['occupancy'], queryFn: async () => (await api.get('/resources/occupancy')).data as any })

  const healthData = (projects.data ?? []).map((p) => ({ name: p.code, members: p.memberCount }))

  const exportCsv = () => {
    const rows = [['Code', 'Name', 'Status', 'Health', 'Priority', 'Client', 'Members']]
    ;(projects.data ?? []).forEach((p) => rows.push([p.code, p.name, p.status, p.health, p.priority, p.clientName, String(p.memberCount)]))
    const csv = rows.map((r) => r.map((c) => `"${(c ?? '').toString().replace(/"/g, '""')}"`).join(',')).join('\n')
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    const a = document.createElement('a'); a.href = url; a.download = 'ariel-vertex-projects.csv'; a.click()
    URL.revokeObjectURL(url); push('Report exported')
  }

  return (
    <div>
      <PageHeader title="Reports" subtitle="Management reporting across projects and resources" icon={<FileBarChart className="h-5 w-5" />}
        actions={<><Button variant="secondary" icon={<Printer className="h-4 w-4" />} onClick={() => window.open('/reports/print', '_blank')}>PDF</Button><Button variant="secondary" icon={<Download className="h-4 w-4" />} onClick={exportCsv}>Export CSV</Button></>} />

      <div className="grid lg:grid-cols-2 gap-4">
        <Card>
          <CardHeader title="Project team sizes" />
          <div className="p-5 h-72">
            {healthData.length ? (
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={healthData}>
                  <CartesianGrid strokeDasharray="3 3" vertical={false} className="stroke-slate-200 dark:stroke-navy-600" />
                  <XAxis dataKey="name" tick={{ fontSize: 12 }} stroke="#94a3b8" />
                  <YAxis allowDecimals={false} tick={{ fontSize: 12 }} stroke="#94a3b8" />
                  <Tooltip />
                  <Bar dataKey="members" fill="#1E7FD4" radius={[6, 6, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            ) : <EmptyState icon={<FolderKanban className="h-6 w-6" />} title="No project data" />}
          </div>
        </Card>

        <Card>
          <CardHeader title="Resource availability" subtitle="Across the organization" />
          <div className="p-5 space-y-2.5">
            {occupancy.data?.buckets?.map((b: any) => (
              <div key={b.bucket} className="flex items-center justify-between rounded-xl border border-[var(--line)] px-4 py-3">
                <span className="text-sm font-medium">{b.bucket}</span>
                <span className="text-lg font-extrabold">{b.count}</span>
              </div>
            ))}
          </div>
        </Card>
      </div>

      <Card className="mt-4">
        <CardHeader title="Project register" subtitle="Snapshot used for the CSV export" />
        <div className="overflow-x-auto av-scroll-x">
          <table className="w-full text-sm">
            <thead><tr className="text-left text-xs uppercase tracking-wide text-slate-400 border-b border-[var(--line)]">
              <th className="px-5 py-3">Code</th><th className="px-5 py-3">Project</th><th className="px-5 py-3">Status</th><th className="px-5 py-3">Health</th><th className="px-5 py-3">Members</th>
            </tr></thead>
            <tbody>
              {projects.data?.map((p) => (
                <tr key={p.id} className="border-b border-[var(--line)] last:border-0">
                  <td className="px-5 py-2.5 font-bold">{p.code}</td><td className="px-5 py-2.5">{p.name}</td>
                  <td className="px-5 py-2.5"><StatusPill value={p.status} /></td><td className="px-5 py-2.5"><StatusPill value={p.health} /></td>
                  <td className="px-5 py-2.5">{p.memberCount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      <p className="mt-4 text-xs text-slate-400">Monthly/quarterly performance-report generation lives under <span className="font-semibold">Performance Reports</span>. Use <span className="font-semibold">PDF</span> for a print-ready management summary, or <span className="font-semibold">Export CSV</span> for the raw project register.</p>
    </div>
  )
}
