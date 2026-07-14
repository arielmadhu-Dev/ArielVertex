import { useState, useEffect } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { motion, AnimatePresence } from 'framer-motion'
import { Menu, X, Search, Sun, Moon, LogOut, ChevronDown, Command } from 'lucide-react'
import { useAuth } from '../lib/auth'
import { useFeatures } from '../lib/hooks'
import { Logo } from '../ui/Logo'
import { Avatar } from '../ui/primitives'
import { cx } from '../ui/util'
import { visibleGroups } from './nav'
import { NotificationBell } from './NotificationBell'
import { CommandPalette } from './CommandPalette'

function useTheme() {
  const [dark, setDark] = useState(() => document.documentElement.classList.contains('dark'))
  const toggle = () => {
    const next = !dark
    setDark(next)
    document.documentElement.classList.toggle('dark', next)
    localStorage.setItem('av-theme', next ? 'dark' : 'light')
  }
  return { dark, toggle }
}

function SidebarContent({ onNavigate }: { onNavigate?: () => void }) {
  const { user } = useAuth()
  const { data: features } = useFeatures()
  if (!user) return null
  return (
    <div className="flex h-full flex-col">
      <div className="px-5 h-16 flex items-center border-b border-[var(--line)]">
        <Logo />
      </div>
      <nav className="flex-1 overflow-y-auto px-3 py-4 space-y-6">
        {visibleGroups(user, features).map((g) => (
          <div key={g.title}>
            <p className="px-3 mb-1.5 text-[10px] font-bold uppercase tracking-[0.12em] text-slate-400">{g.title}</p>
            <div className="space-y-0.5">
              {g.items.map((item) => (
                <NavLink key={item.to} to={item.to} end={item.to === '/'} onClick={onNavigate}
                  className={({ isActive }) => cx(
                    'group relative flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-semibold transition',
                    isActive ? 'text-brand-700 dark:text-white bg-brand-50 dark:bg-brand-900/40'
                      : 'text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-navy-600')}>
                  {({ isActive }) => (
                    <>
                      {isActive && <motion.span layoutId="nav-active" className="absolute left-0 top-1.5 bottom-1.5 w-1 rounded-full bg-brand-500" />}
                      <item.icon className="h-[18px] w-[18px] shrink-0" />
                      <span className="truncate">{item.label}</span>
                    </>
                  )}
                </NavLink>
              ))}
            </div>
          </div>
        ))}
      </nav>
      <div className="p-3 border-t border-[var(--line)]">
        <div className="flex items-center gap-3 rounded-xl px-2 py-2">
          <Avatar name={user.name} color={user.avatarColor} size={38} />
          <div className="min-w-0 flex-1">
            <p className="text-sm font-bold truncate">{user.name}</p>
            <p className="text-xs text-slate-500 truncate">{user.roleLabel}</p>
          </div>
        </div>
      </div>
    </div>
  )
}

export function AppLayout() {
  const { user, logout } = useAuth()
  const { dark, toggle } = useTheme()
  const [mobileOpen, setMobileOpen] = useState(false)
  const [cmdOpen, setCmdOpen] = useState(false)
  const [menuOpen, setMenuOpen] = useState(false)
  const loc = useLocation()
  const navigate = useNavigate()

  useEffect(() => { setMobileOpen(false); setMenuOpen(false) }, [loc.pathname])
  useEffect(() => {
    const h = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); setCmdOpen((o) => !o) }
    }
    window.addEventListener('keydown', h)
    return () => window.removeEventListener('keydown', h)
  }, [])

  if (!user) return null

  return (
    <div className="min-h-screen av-mesh">
      {/* Desktop sidebar */}
      <aside className="hidden lg:flex fixed inset-y-0 left-0 w-[268px] flex-col border-r border-[var(--line)] bg-[var(--card)]/80 backdrop-blur z-30">
        <SidebarContent />
      </aside>

      {/* Mobile drawer */}
      <AnimatePresence>
        {mobileOpen && (
          <div className="lg:hidden fixed inset-0 z-50">
            <motion.div className="absolute inset-0 bg-navy-900/50 backdrop-blur-sm" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} onClick={() => setMobileOpen(false)} />
            <motion.aside className="absolute inset-y-0 left-0 w-[280px] bg-[var(--card)] shadow-pop"
              initial={{ x: -300 }} animate={{ x: 0 }} exit={{ x: -300 }} transition={{ type: 'spring', damping: 26, stiffness: 240 }}>
              <button onClick={() => setMobileOpen(false)} className="absolute right-3 top-4 text-slate-400"><X className="h-5 w-5" /></button>
              <SidebarContent onNavigate={() => setMobileOpen(false)} />
            </motion.aside>
          </div>
        )}
      </AnimatePresence>

      <div className="lg:pl-[268px]">
        {/* Topbar */}
        <header className="sticky top-0 z-20 h-16 flex items-center gap-3 px-4 sm:px-6 border-b border-[var(--line)] bg-[var(--surface)]/80 backdrop-blur">
          <button onClick={() => setMobileOpen(true)} className="lg:hidden text-slate-500 p-1"><Menu className="h-6 w-6" /></button>

          <button onClick={() => setCmdOpen(true)}
            className="group flex items-center gap-2.5 rounded-xl border border-[var(--line)] bg-[var(--card)] px-3 py-2 text-sm text-slate-400 hover:text-slate-600 transition w-full max-w-xs">
            <Search className="h-4 w-4" />
            <span className="flex-1 text-left">Search or jump to…</span>
            <kbd className="hidden sm:inline-flex items-center gap-0.5 rounded-md border border-[var(--line)] px-1.5 py-0.5 text-[10px] font-semibold"><Command className="h-3 w-3" />K</kbd>
          </button>

          <div className="ml-auto flex items-center gap-1.5">
            <button onClick={toggle} className="grid place-items-center h-9 w-9 rounded-xl hover:bg-slate-100 dark:hover:bg-navy-600 text-slate-500">
              {dark ? <Sun className="h-[18px] w-[18px]" /> : <Moon className="h-[18px] w-[18px]" />}
            </button>
            <NotificationBell />
            <div className="relative">
              <button onClick={() => setMenuOpen((o) => !o)} className="flex items-center gap-2 rounded-xl pl-1 pr-2 py-1 hover:bg-slate-100 dark:hover:bg-navy-600">
                <Avatar name={user.name} color={user.avatarColor} size={32} />
                <ChevronDown className="h-4 w-4 text-slate-400" />
              </button>
              <AnimatePresence>
                {menuOpen && (
                  <motion.div initial={{ opacity: 0, y: -6, scale: 0.97 }} animate={{ opacity: 1, y: 0, scale: 1 }} exit={{ opacity: 0, y: -6, scale: 0.97 }}
                    className="absolute right-0 mt-2 w-60 av-card shadow-pop p-1.5 z-30">
                    <div className="px-3 py-2.5">
                      <p className="font-bold text-sm">{user.name}</p>
                      <p className="text-xs text-slate-500">{user.email}</p>
                      <p className="text-xs text-brand-600 dark:text-brand-300 font-semibold mt-1">{user.roleLabel} · {user.employeeCode}</p>
                    </div>
                    <button onClick={() => { logout(); navigate('/login') }}
                      className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-sm font-semibold text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-900/30">
                      <LogOut className="h-4 w-4" /> Sign out
                    </button>
                  </motion.div>
                )}
              </AnimatePresence>
            </div>
          </div>
        </header>

        <main className="px-4 sm:px-6 py-6 max-w-[1400px] mx-auto">
          <Outlet />
        </main>
      </div>

      <CommandPalette open={cmdOpen} onClose={() => setCmdOpen(false)} />
    </div>
  )
}
