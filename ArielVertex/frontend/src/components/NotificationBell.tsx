import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { Bell, CheckCheck } from 'lucide-react'
import { api } from '../lib/api'
import type { NotificationItem } from '../lib/types'
import { relTime } from '../ui/util'

export function NotificationBell() {
  const [open, setOpen] = useState(false)
  const qc = useQueryClient()
  const navigate = useNavigate()

  const { data } = useQuery({
    queryKey: ['notifications'],
    queryFn: async () => (await api.get<{ items: NotificationItem[]; unread: number }>('/notifications')).data,
    refetchInterval: 30_000,
  })

  const markAll = useMutation({
    mutationFn: () => api.post('/notifications/read-all'),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['notifications'] }),
  })
  const markOne = useMutation({
    mutationFn: (id: number) => api.post(`/notifications/${id}/read`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['notifications'] }),
  })

  const unread = data?.unread ?? 0
  const items = data?.items ?? []

  return (
    <div className="relative">
      <button onClick={() => setOpen((o) => !o)} className="relative grid place-items-center h-9 w-9 rounded-xl hover:bg-slate-100 dark:hover:bg-navy-600 text-slate-500">
        <Bell className="h-[18px] w-[18px]" />
        {unread > 0 && (
          <span className="absolute -top-0.5 -right-0.5 grid place-items-center min-w-[17px] h-[17px] px-1 rounded-full bg-rose-500 text-white text-[10px] font-bold ring-2 ring-[var(--surface)]">
            {unread > 9 ? '9+' : unread}
          </span>
        )}
      </button>
      <AnimatePresence>
        {open && (
          <>
            <div className="fixed inset-0 z-30" onClick={() => setOpen(false)} />
            <motion.div initial={{ opacity: 0, y: -6, scale: 0.97 }} animate={{ opacity: 1, y: 0, scale: 1 }} exit={{ opacity: 0, y: -6, scale: 0.97 }}
              className="absolute right-0 mt-2 w-[360px] max-w-[92vw] av-card shadow-pop z-40 overflow-hidden">
              <div className="flex items-center justify-between px-4 py-3 border-b border-[var(--line)]">
                <p className="font-bold text-sm">Notifications</p>
                {unread > 0 && (
                  <button onClick={() => markAll.mutate()} className="inline-flex items-center gap-1 text-xs font-semibold text-brand-600 hover:text-brand-700">
                    <CheckCheck className="h-3.5 w-3.5" /> Mark all read
                  </button>
                )}
              </div>
              <div className="max-h-[380px] overflow-y-auto">
                {items.length === 0 && <p className="text-sm text-slate-400 text-center py-10">You're all caught up.</p>}
                {items.map((n) => (
                  <button key={n.id}
                    onClick={() => { markOne.mutate(n.id); if (n.link) navigate(n.link); setOpen(false) }}
                    className="w-full text-left flex gap-3 px-4 py-3 border-b border-[var(--line)] hover:bg-slate-50 dark:hover:bg-navy-600 transition">
                    <span className={`mt-1.5 h-2 w-2 rounded-full shrink-0 ${n.isRead ? 'bg-transparent' : 'bg-brand-500'}`} />
                    <div className="min-w-0">
                      <p className="text-sm font-semibold truncate">{n.title}</p>
                      <p className="text-xs text-slate-500 line-clamp-2">{n.message}</p>
                      <p className="text-[11px] text-slate-400 mt-1">{relTime(n.createdAt)}</p>
                    </div>
                  </button>
                ))}
              </div>
            </motion.div>
          </>
        )}
      </AnimatePresence>
    </div>
  )
}
