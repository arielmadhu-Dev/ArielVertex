import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Bell, CheckCheck } from 'lucide-react'
import { api } from '../lib/api'
import type { NotificationItem } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, Button, EmptyState } from '../ui/primitives'
import { relTime } from '../ui/util'

export default function NotificationsPage() {
  const qc = useQueryClient()
  const navigate = useNavigate()
  const { data } = useQuery({ queryKey: ['notifications'], queryFn: async () => (await api.get<{ items: NotificationItem[]; unread: number }>('/notifications')).data })
  const markAll = useMutation({ mutationFn: () => api.post('/notifications/read-all'), onSuccess: () => qc.invalidateQueries({ queryKey: ['notifications'] }) })
  const markOne = useMutation({ mutationFn: (id: number) => api.post(`/notifications/${id}/read`), onSuccess: () => qc.invalidateQueries({ queryKey: ['notifications'] }) })

  return (
    <div>
      <PageHeader title="Notifications" subtitle="Your portal alerts (Teams/Outlook delivery adds on when enabled)" icon={<Bell className="h-5 w-5" />}
        actions={data?.unread ? <Button variant="secondary" icon={<CheckCheck className="h-4 w-4" />} onClick={() => markAll.mutate()}>Mark all read</Button> : undefined} />
      <Card className="overflow-hidden">
        {data?.items.length ? data.items.map((n) => (
          <button key={n.id} onClick={() => { markOne.mutate(n.id); if (n.link) navigate(n.link) }}
            className="w-full text-left flex gap-3 px-5 py-4 border-b border-[var(--line)] last:border-0 hover:bg-slate-50 dark:hover:bg-navy-600">
            <span className={`mt-1.5 h-2.5 w-2.5 rounded-full shrink-0 ${n.isRead ? 'bg-slate-200 dark:bg-navy-500' : 'bg-brand-500'}`} />
            <div className="min-w-0 flex-1">
              <p className="font-semibold text-sm">{n.title}</p>
              <p className="text-sm text-slate-500">{n.message}</p>
            </div>
            <span className="text-xs text-slate-400 whitespace-nowrap">{relTime(n.createdAt)}</span>
          </button>
        )) : <EmptyState icon={<Bell className="h-6 w-6" />} title="You're all caught up" hint="New alerts will show up here." />}
      </Card>
    </div>
  )
}
