import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { PieChart, Pie, Cell, ResponsiveContainer, Tooltip } from 'recharts'
import { CalendarClock, Activity, ArrowUpRight, ChevronRight, Sparkles } from 'lucide-react'
import { api } from '../lib/api'
import { useAuth } from '../lib/auth'
import type { Dashboard as Dash } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, CardHeader, StatusPill, EmptyState, Skeleton } from '../ui/primitives'
import { StatCard } from '../ui/widgets'
import { fmtDateTime, relTime, Tone } from '../ui/util'

const OCC_COLORS: Record<string, string> = {
  'Free': '#94a3b8', 'Partially Available': '#1E7FD4', 'Fully Allocated': '#10b981', 'Overloaded': '#f43f5e',
}
const healthTone = (h: string): Tone => (h === 'Green' ? 'good' : h === 'Amber' ? 'warn' : 'danger')

export default function Dashboard() {
  const { user } = useAuth()
  const { data, isLoading } = useQuery({ queryKey: ['dashboard'], queryFn: async () => (await api.get<Dash>('/dashboard')).data })

  const hour = new Date().getHours()
  const greeting = hour < 12 ? 'Good morning' : hour < 17 ? 'Good afternoon' : 'Good evening'

  return (
    <div>
      <PageHeader title={`${greeting}, ${user?.name.split(' ')[0]}`} subtitle="Here's what's happening across your workspace today." />

      {/* Stats */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4">
        {isLoading ? [...Array(4)].map((_, i) => <Skeleton key={i} className="h-[104px]" />)
          : data?.stats.map((s, i) => <StatCard key={s.key} label={s.label} value={s.value} delta={s.delta ?? undefined} t={s.tone as Tone} icon={s.icon} index={i} />)}
      </div>

      <div className="grid lg:grid-cols-3 gap-4 mt-4">
        {/* Project health */}
        <Card className="lg:col-span-2 overflow-hidden">
          <CardHeader title="Project Health" subtitle="Status and missing-update signals across your projects" icon={<Activity className="h-[18px] w-[18px]" />} />
          <div className="divide-y divide-[var(--line)]">
            {isLoading ? <div className="p-5 space-y-3">{[...Array(3)].map((_, i) => <Skeleton key={i} className="h-10" />)}</div>
              : data?.projectHealth.length ? data.projectHealth.map((p) => (
                <Link key={p.projectId} to={`/projects/${p.projectId}`} className="flex items-center gap-4 px-5 py-3.5 hover:bg-slate-50 dark:hover:bg-navy-600 transition group">
                  <div className="grid place-items-center h-9 w-9 rounded-xl bg-brand-50 dark:bg-brand-900/40 text-brand-600 font-bold text-xs">{p.code}</div>
                  <div className="min-w-0 flex-1">
                    <p className="font-semibold truncate">{p.name}</p>
                    <p className="text-xs text-slate-500">{p.missingUpdates > 0 ? `${p.missingUpdates} missing update${p.missingUpdates > 1 ? 's' : ''} today` : 'All updates in'}</p>
                  </div>
                  <StatusPill value={p.health} />
                  <ChevronRight className="h-4 w-4 text-slate-300 group-hover:text-brand-500" />
                </Link>
              )) : <EmptyState icon={<Activity className="h-6 w-6" />} title="No projects to show" hint="Projects you can access will appear here." />}
          </div>
        </Card>

        {/* Resource occupancy or upcoming for non-mgmt */}
        {data && data.resourceOccupancy.length > 0 ? (
          <Card>
            <CardHeader title="Resource Occupancy" subtitle="Allocation across the org" />
            <div className="p-5">
              <div className="h-44">
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie data={data.resourceOccupancy} dataKey="count" nameKey="bucket" innerRadius={44} outerRadius={70} paddingAngle={3}>
                      {data.resourceOccupancy.map((e) => <Cell key={e.bucket} fill={OCC_COLORS[e.bucket] ?? '#94a3b8'} />)}
                    </Pie>
                    <Tooltip />
                  </PieChart>
                </ResponsiveContainer>
              </div>
              <div className="mt-2 space-y-1.5">
                {data.resourceOccupancy.map((e) => (
                  <div key={e.bucket} className="flex items-center gap-2 text-sm">
                    <span className="h-2.5 w-2.5 rounded-full" style={{ background: OCC_COLORS[e.bucket] }} />
                    <span className="flex-1 text-slate-600 dark:text-slate-300">{e.bucket}</span>
                    <span className="font-bold">{e.count}</span>
                  </div>
                ))}
              </div>
            </div>
          </Card>
        ) : (
          <Card>
            <CardHeader title="Upcoming" icon={<CalendarClock className="h-[18px] w-[18px]" />} />
            <UpcomingList data={data} isLoading={isLoading} />
          </Card>
        )}
      </div>

      <div className="grid lg:grid-cols-3 gap-4 mt-4">
        {/* Pending / action items */}
        <Card className="lg:col-span-2">
          <CardHeader title="Needs your attention" icon={<Sparkles className="h-[18px] w-[18px]" />} />
          <div className="p-4 grid sm:grid-cols-2 gap-3">
            {data?.pending.length ? data.pending.map((p, i) => (
              <Link key={i} to={p.link || '#'} className="group rounded-xl border border-[var(--line)] p-4 hover:border-brand-300 hover:shadow-card transition">
                <div className="flex items-start justify-between">
                  <span className="text-3xl font-extrabold text-brand-600">{p.count}</span>
                  <ArrowUpRight className="h-4 w-4 text-slate-300 group-hover:text-brand-500" />
                </div>
                <p className="mt-1 font-semibold text-sm">{p.title}</p>
                <p className="text-xs text-slate-500">{p.detail}</p>
              </Link>
            )) : <div className="sm:col-span-2"><EmptyState icon={<Sparkles className="h-6 w-6" />} title="Nothing pending" hint="You have no outstanding action items." /></div>}
          </div>
        </Card>

        {/* Recent activity */}
        <Card>
          <CardHeader title="Recent Activity" />
          <div className="p-4 space-y-3">
            {data?.recentActivity.length ? data.recentActivity.map((a, i) => (
              <div key={i} className="flex gap-3">
                <div className="mt-1 h-2 w-2 rounded-full bg-brand-400 shrink-0" />
                <div className="min-w-0">
                  <p className="text-sm font-semibold truncate">{a.title}</p>
                  <p className="text-xs text-slate-500 line-clamp-2">{a.detail}</p>
                  <p className="text-[11px] text-slate-400">{relTime(a.when)}</p>
                </div>
              </div>
            )) : <p className="text-sm text-slate-400 text-center py-6">No recent activity.</p>}
          </div>
        </Card>
      </div>
    </div>
  )
}

function UpcomingList({ data, isLoading }: { data?: Dash; isLoading: boolean }) {
  if (isLoading) return <div className="p-5 space-y-3">{[...Array(3)].map((_, i) => <Skeleton key={i} className="h-10" />)}</div>
  if (!data?.upcoming.length) return <EmptyState icon={<CalendarClock className="h-6 w-6" />} title="Nothing scheduled" hint="Upcoming calls and reviews will appear here." />
  return (
    <div className="divide-y divide-[var(--line)]">
      {data.upcoming.map((u, i) => (
        <Link key={i} to={u.link || '#'} className="flex items-center gap-3 px-5 py-3 hover:bg-slate-50 dark:hover:bg-navy-600">
          <div className="grid place-items-center h-9 w-9 rounded-xl bg-brand-50 dark:bg-brand-900/40 text-brand-500"><CalendarClock className="h-4 w-4" /></div>
          <div className="min-w-0 flex-1"><p className="text-sm font-semibold truncate">{u.title}</p><p className="text-xs text-slate-500">{u.kind} · {u.detail}</p></div>
          <span className="text-xs text-slate-400 whitespace-nowrap">{fmtDateTime(u.when)}</span>
        </Link>
      ))}
    </div>
  )
}
