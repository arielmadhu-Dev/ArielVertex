import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { motion } from 'framer-motion'
import { LogIn, ArrowRight, ShieldCheck, Sparkles } from 'lucide-react'
import { useAuth } from '../lib/auth'
import { apiError } from '../lib/api'
import { microsoftEnabled, signInWithMicrosoft } from '../lib/msal'
import { Logo, VertexMark } from '../ui/Logo'
import { Button } from '../ui/primitives'
import { Field, Input } from '../ui/form'

const demoUsers = [
  { name: 'Arc — CEO', email: 'arc@arielsoftwares.in' },
  { name: 'Mareena — HR Manager', email: 'mareena@arielsoftwares.in' },
  { name: 'Surbeen — HR Director', email: 'surbeen@arielsoftwares.in' },
  { name: 'Anjali — HR Recruiter', email: 'anjali@arielsoftwares.in' },
  { name: 'Shepherd — Project Manager', email: 'shepherd@arielsoftwares.in' },
  { name: 'Maria — Project Coordinator', email: 'maria@arielsoftwares.in' },
  { name: 'Arveen — Business Director', email: 'arveen@arielsoftwares.in' },
  { name: 'Nikhil — Team Lead', email: 'nikhil@arielsoftwares.in' },
  { name: 'Rahul — Developer', email: 'rahul@arielsoftwares.in' },
  { name: 'Rajat — System Admin', email: 'rajat@arielsoftwares.in' },
]

export default function Login() {
  const { login, adopt } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('arc@arielsoftwares.in')
  const [password, setPassword] = useState('Ariel@123')
  const [err, setErr] = useState('')
  const [loading, setLoading] = useState(false)
  const [msLoading, setMsLoading] = useState(false)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setErr(''); setLoading(true)
    try { await login(email, password); navigate('/') }
    catch (e) { setErr(apiError(e, 'Unable to sign in.')) }
    finally { setLoading(false) }
  }

  const microsoft = async () => {
    setErr(''); setMsLoading(true)
    try { const u = await signInWithMicrosoft(); adopt(u); navigate('/') }
    catch (e) { setErr(apiError(e, 'Microsoft sign-in failed.')) }
    finally { setMsLoading(false) }
  }

  return (
    <div className="min-h-screen grid lg:grid-cols-2">
      {/* Brand panel */}
      <div className="relative hidden lg:flex flex-col justify-between av-brand-gradient text-white p-12 overflow-hidden">
        <div className="absolute inset-0 opacity-30"
          style={{ backgroundImage: 'radial-gradient(circle at 20% 20%, rgba(255,255,255,.25), transparent 40%), radial-gradient(circle at 80% 70%, rgba(30,127,212,.5), transparent 45%)' }} />
        <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }} className="relative flex items-center gap-3">
          <VertexMark size={38} className="text-white" />
          <span className="text-xl font-extrabold tracking-tight">Ariel <span className="text-brand-200">Vertex</span></span>
        </motion.div>

        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.1 }} className="relative">
          <h1 className="text-4xl font-extrabold leading-tight">Operations, Review &amp;<br />Performance — unified.</h1>
          <p className="mt-4 text-brand-100/90 max-w-md">
            Projects, developer status, reviews, structured feedback and performance reporting in one secure, role-based portal.
          </p>
          <div className="mt-8 space-y-3">
            {[
              { icon: ShieldCheck, t: 'Project-scoped security', d: 'Every record enforces role + project membership.' },
              { icon: Sparkles, t: 'Microsoft 365 ready', d: 'Entra login & Teams/Outlook plug in when enabled.' },
            ].map((f) => (
              <div key={f.t} className="flex items-start gap-3">
                <div className="grid place-items-center h-9 w-9 rounded-xl bg-white/15"><f.icon className="h-5 w-5" /></div>
                <div><p className="font-semibold">{f.t}</p><p className="text-sm text-brand-100/80">{f.d}</p></div>
              </div>
            ))}
          </div>
        </motion.div>

        <p className="relative text-xs text-brand-100/70">© Ariel Software Solutions · Internal use</p>
      </div>

      {/* Form panel */}
      <div className="flex items-center justify-center p-6 av-mesh">
        <motion.div initial={{ opacity: 0, y: 16 }} animate={{ opacity: 1, y: 0 }} className="w-full max-w-sm">
          <div className="lg:hidden mb-8 flex justify-center"><Logo /></div>
          <h2 className="text-2xl font-extrabold">Welcome back</h2>
          <p className="text-sm text-slate-500 mt-1">Sign in to your Ariel Vertex workspace.</p>

          <form onSubmit={submit} className="mt-6 space-y-4">
            <Field label="Work email">
              <Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="you@arielsoftwares.in" autoComplete="username" required />
            </Field>
            <Field label="Password">
              <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" required />
            </Field>
            {err && <p className="text-sm font-medium text-rose-500 bg-rose-50 dark:bg-rose-900/30 rounded-lg px-3 py-2">{err}</p>}
            <Button type="submit" loading={loading} icon={<LogIn className="h-4 w-4" />} className="w-full">Sign in</Button>
          </form>

          {microsoftEnabled && (
            <>
              <div className="my-4 flex items-center gap-3 text-xs text-slate-400"><span className="h-px flex-1 bg-[var(--line)]" />OR<span className="h-px flex-1 bg-[var(--line)]" /></div>
              <Button variant="secondary" loading={msLoading} onClick={microsoft} className="w-full"
                icon={<svg width="16" height="16" viewBox="0 0 23 23" aria-hidden><path fill="#f25022" d="M1 1h10v10H1z" /><path fill="#7fba00" d="M12 1h10v10H12z" /><path fill="#00a4ef" d="M1 12h10v10H1z" /><path fill="#ffb900" d="M12 12h10v10H12z" /></svg>}>
                Sign in with Microsoft
              </Button>
            </>
          )}

          <div className="mt-6 rounded-2xl border border-[var(--line)] bg-[var(--card)] p-3">
            <p className="text-xs font-semibold text-slate-500 mb-2 flex items-center gap-1.5"><Sparkles className="h-3.5 w-3.5 text-brand-500" /> Demo accounts (password: Ariel@123)</p>
            <div className="grid grid-cols-1 gap-1 max-h-40 overflow-y-auto">
              {demoUsers.map((u) => (
                <button key={u.email} onClick={() => { setEmail(u.email); setPassword('Ariel@123') }}
                  className="group flex items-center justify-between rounded-lg px-2.5 py-1.5 text-left text-sm hover:bg-brand-50 dark:hover:bg-brand-900/30 transition">
                  <span className="font-medium truncate">{u.name}</span>
                  <ArrowRight className="h-3.5 w-3.5 text-slate-300 group-hover:text-brand-500" />
                </button>
              ))}
            </div>
          </div>
        </motion.div>
      </div>
    </div>
  )
}
