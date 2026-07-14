import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { createPortal } from 'react-dom'
import { useQuery } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { Search, CornerDownLeft, LucideIcon } from 'lucide-react'
import { useAuth } from '../lib/auth'
import { useFeatures } from '../lib/hooks'
import { api } from '../lib/api'
import type { SearchResult } from '../lib/types'
import { visibleGroups } from './nav'

interface Item { key: string; label: string; group: string; to: string; Icon?: LucideIcon }

export function CommandPalette({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { user } = useAuth()
  const { data: features } = useFeatures()
  const navigate = useNavigate()
  const [q, setQ] = useState('')
  const [debounced, setDebounced] = useState('')
  const [active, setActive] = useState(0)
  const inputRef = useRef<HTMLInputElement>(null)

  const commands = useMemo<Item[]>(() => {
    if (!user) return []
    return visibleGroups(user, features).flatMap((g) => g.items.map((i) => ({ key: `nav:${i.to}`, label: i.label, group: g.title, to: i.to, Icon: i.icon })))
  }, [user, features])

  const navMatches = useMemo(() => {
    const s = q.trim().toLowerCase()
    return s ? commands.filter((c) => c.label.toLowerCase().includes(s) || c.group.toLowerCase().includes(s)) : commands
  }, [q, commands])

  // Debounced backend global search (people, projects, goals, promotions, training).
  useEffect(() => { const t = setTimeout(() => setDebounced(q.trim()), 220); return () => clearTimeout(t) }, [q])
  const search = useQuery({
    queryKey: ['search', debounced],
    queryFn: async () => (await api.get<SearchResult[]>('/search', { params: { q: debounced } })).data,
    enabled: open && debounced.length >= 2,
    staleTime: 15_000,
  })

  const resultItems = useMemo<Item[]>(
    () => (search.data ?? []).map((r, i) => ({ key: `res:${r.type}:${r.id}:${i}`, label: r.title, group: `${r.type} · ${r.subtitle}`.trim(), to: r.link })),
    [search.data],
  )

  const items = useMemo(() => [...navMatches, ...resultItems], [navMatches, resultItems])

  useEffect(() => { if (open) { setQ(''); setDebounced(''); setActive(0); setTimeout(() => inputRef.current?.focus(), 40) } }, [open])
  useEffect(() => { setActive(0) }, [items.length])

  const go = (to: string) => { navigate(to); onClose() }

  const onKey = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') { e.preventDefault(); setActive((a) => Math.min(a + 1, items.length - 1)) }
    if (e.key === 'ArrowUp') { e.preventDefault(); setActive((a) => Math.max(a - 1, 0)) }
    if (e.key === 'Enter' && items[active]) { e.preventDefault(); go(items[active].to) }
  }

  return createPortal(
    <AnimatePresence>
      {open && (
        <div className="fixed inset-0 z-[70] flex items-start justify-center p-4 pt-[12vh]">
          <motion.div className="fixed inset-0 bg-navy-900/50 backdrop-blur-sm" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} onClick={onClose} />
          <motion.div className="av-card shadow-pop relative z-10 w-full max-w-lg overflow-hidden"
            initial={{ opacity: 0, y: -12, scale: 0.98 }} animate={{ opacity: 1, y: 0, scale: 1 }} exit={{ opacity: 0, y: -12, scale: 0.98 }}>
            <div className="flex items-center gap-3 px-4 border-b border-[var(--line)]">
              <Search className="h-5 w-5 text-slate-400" />
              <input ref={inputRef} value={q} onChange={(e) => setQ(e.target.value)} onKeyDown={onKey}
                placeholder="Search people, projects, goals… or jump to a section" className="flex-1 bg-transparent py-4 outline-none text-sm" />
            </div>
            <div className="max-h-[320px] overflow-y-auto p-2">
              {items.length === 0 && <p className="text-sm text-slate-400 text-center py-8">{search.isFetching ? 'Searching…' : 'No matches.'}</p>}
              {items.map((c, i) => (
                <button key={c.key} onMouseEnter={() => setActive(i)} onClick={() => go(c.to)}
                  className={`w-full flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm ${i === active ? 'bg-brand-50 dark:bg-brand-900/40 text-brand-700 dark:text-white' : 'hover:bg-slate-50 dark:hover:bg-navy-600'}`}>
                  {c.Icon ? <c.Icon className="h-[18px] w-[18px] text-slate-400" /> : <Search className="h-[18px] w-[18px] text-slate-300" />}
                  <span className="font-semibold truncate">{c.label}</span>
                  <span className="text-xs text-slate-400 ml-1 truncate">{c.group}</span>
                  {i === active && <CornerDownLeft className="h-4 w-4 ml-auto shrink-0 text-slate-400" />}
                </button>
              ))}
            </div>
          </motion.div>
        </div>
      )}
    </AnimatePresence>,
    document.body,
  )
}
