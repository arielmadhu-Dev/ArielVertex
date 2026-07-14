import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Wallet, Plus, Settings, Download, Upload, CheckCircle2, XCircle, IndianRupee, ReceiptText } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import { useEnums } from '../lib/hooks'
import type { ExpenseItem, ExpenseSummary, ExpenseSettings } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, StatusPill, Badge, Button, EmptyState, Skeleton } from '../ui/primitives'
import { StatCard } from '../ui/widgets'
import { Modal } from '../ui/Modal'
import { Field, Input, Select, Textarea } from '../ui/form'
import { useToast } from '../ui/Toast'
import { fmtDate } from '../ui/util'
import { P } from '../components/nav'

const inr = (n: number) => '₹' + n.toLocaleString('en-IN')

export default function Expenses() {
  const { has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const enums = useEnums()
  const [open, setOpen] = useState(false)
  const [settingsOpen, setSettingsOpen] = useState(false)
  const canManage = has(P.ExpensesManage)
  const canConfigure = has(P.ExpensesConfigure)

  const { data, isLoading } = useQuery({
    queryKey: ['expenses'],
    queryFn: async () => (await api.get<{ items: ExpenseItem[]; summary: ExpenseSummary }>('/expenses')).data,
  })
  const invalidate = () => qc.invalidateQueries({ queryKey: ['expenses'] })

  const [form, setForm] = useState({ title: '', category: 'OfficeSupplies', amount: '', vendor: '', expenseDate: new Date().toISOString().slice(0, 10), paymentMethod: 'UPI', invoiceNumber: '', approvalRequired: true, description: '' })
  const create = useMutation({
    mutationFn: () => api.post('/expenses', { ...form, amount: Number(form.amount) }),
    onSuccess: () => { push('Expense raised'); setOpen(false); invalidate(); setForm({ ...form, title: '', amount: '', vendor: '', invoiceNumber: '', description: '' }) },
    onError: (e) => push(apiError(e), 'error'),
  })
  const act = useMutation({
    mutationFn: ({ id, action, note }: { id: number; action: string; note?: string }) => api.post(`/expenses/${id}/${action}`, note !== undefined ? { note } : {}),
    onSuccess: (_d, v) => { push(`Expense ${v.action === 'pay' ? 'marked paid' : v.action + 'd'}`); invalidate() },
    onError: (e) => push(apiError(e), 'error'),
  })

  const uploadInvoice = async (id: number, file: File) => {
    try { const fd = new FormData(); fd.append('file', file); await api.post(`/expenses/${id}/invoice`, fd); push('Invoice attached'); invalidate() }
    catch (e) { push(apiError(e), 'error') }
  }
  const download = async (id: number, name?: string) => {
    try { const r = await api.get(`/expenses/${id}/invoice/download`, { responseType: 'blob' }); const url = URL.createObjectURL(r.data); const a = document.createElement('a'); a.href = url; a.download = name || 'invoice'; a.click(); URL.revokeObjectURL(url) }
    catch (e) { push(apiError(e), 'error') }
  }

  const s = data?.summary
  return (
    <div>
      <PageHeader title="Expenses" subtitle="Internal company expenses & payment requests (Front Desk)" icon={<Wallet className="h-5 w-5" />}
        actions={<>
          {canConfigure && <Button variant="secondary" icon={<Settings className="h-4 w-4" />} onClick={() => setSettingsOpen(true)}>Settings</Button>}
          {canManage && <Button icon={<Plus className="h-4 w-4" />} onClick={() => setOpen(true)}>New expense</Button>}
        </>} />

      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 mb-4">
        <StatCard label="Expenses" value={String(s?.total ?? 0)} t="brand" icon="folder" index={0} />
        <StatCard label="Awaiting approval" value={String(s?.pendingApproval ?? 0)} t={s?.pendingApproval ? 'warn' : 'good'} icon="clipboard" index={1} />
        <StatCard label="Paid" value={String(s?.paid ?? 0)} t="good" icon="check" index={2} />
        <StatCard label="Total value" value={inr(s?.totalAmount ?? 0)} t="info" icon="trending" index={3} />
      </div>

      <Card className="overflow-hidden">
        <div className="overflow-x-auto av-scroll-x">
          <table className="w-full text-sm">
            <thead><tr className="text-left text-xs uppercase tracking-wide text-slate-400 border-b border-[var(--line)]">
              <th className="px-5 py-3">Expense</th><th className="px-5 py-3">Category</th><th className="px-5 py-3 text-right">Amount</th>
              <th className="px-5 py-3">Status</th><th className="px-5 py-3">Invoice</th><th className="px-5 py-3">Raised by</th><th className="px-5 py-3 text-right">Actions</th>
            </tr></thead>
            <tbody>
              {isLoading ? [...Array(4)].map((_, i) => <tr key={i}><td colSpan={7} className="px-5 py-2"><Skeleton className="h-9" /></td></tr>)
                : data?.items.length ? data.items.map((e) => (
                  <tr key={e.id} className="border-b border-[var(--line)] last:border-0 hover:bg-slate-50 dark:hover:bg-navy-600">
                    <td className="px-5 py-3"><p className="font-semibold">{e.title}</p><p className="text-xs text-slate-400">{e.vendor || '—'} · {fmtDate(e.expenseDate)}{e.approvalRequired && <span className="ml-1 text-amber-500">· approval required</span>}</p></td>
                    <td className="px-5 py-3"><Badge t="info">{e.categoryLabel}</Badge></td>
                    <td className="px-5 py-3 text-right font-bold whitespace-nowrap tabular-nums">{inr(e.amount)}</td>
                    <td className="px-5 py-3"><StatusPill value={e.status} /></td>
                    <td className="px-5 py-3">
                      {e.hasInvoiceFile
                        ? <button onClick={() => download(e.id, e.invoiceFileName)} className="inline-flex items-center gap-1 text-brand-500 hover:text-brand-600 text-xs font-semibold"><Download className="h-3.5 w-3.5" />{e.invoiceNumber || 'file'}</button>
                        : e.canManage
                          ? <label className="inline-flex items-center gap-1 text-slate-400 hover:text-brand-500 text-xs font-semibold cursor-pointer"><Upload className="h-3.5 w-3.5" />Attach<input type="file" className="hidden" onChange={(ev) => ev.target.files?.[0] && uploadInvoice(e.id, ev.target.files[0])} /></label>
                          : <span className="text-xs text-slate-400">{e.invoiceNumber || '—'}</span>}
                    </td>
                    <td className="px-5 py-3 text-slate-500">{e.raisedByName}</td>
                    <td className="px-5 py-3">
                      <div className="flex items-center gap-1.5 justify-end">
                        {e.canApprove && <>
                          <Button size="sm" variant="secondary" icon={<CheckCircle2 className="h-4 w-4" />} onClick={() => act.mutate({ id: e.id, action: 'approve', note: 'Approved' })}>Approve</Button>
                          <Button size="sm" variant="ghost" icon={<XCircle className="h-4 w-4" />} onClick={() => { const n = prompt('Reason for rejection?') ?? ''; act.mutate({ id: e.id, action: 'reject', note: n }) }}>Reject</Button>
                        </>}
                        {e.canManage && e.status !== 'Paid' && e.status !== 'Rejected' && (!e.approvalRequired || e.status === 'Approved') &&
                          <Button size="sm" icon={<IndianRupee className="h-4 w-4" />} onClick={() => act.mutate({ id: e.id, action: 'pay' })}>Mark paid</Button>}
                      </div>
                    </td>
                  </tr>
                )) : <tr><td colSpan={7}><EmptyState icon={<ReceiptText className="h-6 w-6" />} title="No expenses yet" hint="Raise a payment request to get started." /></td></tr>}
            </tbody>
          </table>
        </div>
      </Card>

      {/* New expense */}
      <Modal open={open} onClose={() => setOpen(false)} title="New expense / payment request"
        footer={<><Button variant="secondary" onClick={() => setOpen(false)}>Cancel</Button><Button loading={create.isPending} disabled={!form.title || !form.amount} onClick={() => create.mutate()}>Raise request</Button></>}>
        <div className="grid sm:grid-cols-2 gap-4">
          <div className="sm:col-span-2"><Field label="Title" required><Input value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} placeholder="e.g. Office stationery" /></Field></div>
          <Field label="Category"><Select value={form.category} onChange={(e) => setForm({ ...form, category: e.target.value })}>{enums.data?.expenseCategories.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select></Field>
          <Field label="Amount (₹)" required><Input type="number" min={0} value={form.amount} onChange={(e) => setForm({ ...form, amount: e.target.value })} /></Field>
          <Field label="Vendor / payee"><Input value={form.vendor} onChange={(e) => setForm({ ...form, vendor: e.target.value })} /></Field>
          <Field label="Expense date"><Input type="date" value={form.expenseDate} onChange={(e) => setForm({ ...form, expenseDate: e.target.value })} /></Field>
          <Field label="Payment method"><Input value={form.paymentMethod} onChange={(e) => setForm({ ...form, paymentMethod: e.target.value })} placeholder="UPI / Card / Bank" /></Field>
          <Field label="Invoice number" hint="Attach the proof file after saving"><Input value={form.invoiceNumber} onChange={(e) => setForm({ ...form, invoiceNumber: e.target.value })} /></Field>
          <div className="sm:col-span-2"><Field label="Notes"><Textarea value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></Field></div>
          <div className="sm:col-span-2"><label className="flex items-center gap-2 text-sm font-medium"><input type="checkbox" checked={form.approvalRequired} onChange={(e) => setForm({ ...form, approvalRequired: e.target.checked })} /> Requires approval before payment</label></div>
        </div>
      </Modal>

      {settingsOpen && <SettingsModal onClose={() => setSettingsOpen(false)} push={push} />}
    </div>
  )
}

function SettingsModal({ onClose, push }: { onClose: () => void; push: (m: string, k?: any) => void }) {
  const qc = useQueryClient()
  const { data } = useQuery({ queryKey: ['expense-settings'], queryFn: async () => (await api.get<ExpenseSettings>('/expense-settings')).data })
  const [form, setForm] = useState<ExpenseSettings | null>(null)
  const s = form ?? data
  const roles = ['HrDirector', 'Accountant', 'CeoAdmin']
  const toggleRole = (r: string) => { if (!s) return; const has = s.approverRoles.includes(r); setForm({ ...s, approverRoles: has ? s.approverRoles.filter((x) => x !== r) : [...s.approverRoles, r] }) }
  const save = useMutation({
    mutationFn: () => api.put('/expense-settings', s),
    onSuccess: () => { push('Settings saved'); qc.invalidateQueries({ queryKey: ['expense-settings'] }); onClose() },
    onError: (e) => push(apiError(e), 'error'),
  })
  return (
    <Modal open onClose={onClose} title="Expense settings" subtitle="Configure approval + who gets the summaries"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={save.isPending} disabled={!s} onClick={() => save.mutate()}>Save</Button></>}>
      {!s ? <Skeleton className="h-40" /> : (
        <div className="space-y-4">
          <label className="flex items-center gap-2 text-sm font-medium"><input type="checkbox" checked={s.approvalRequiredByDefault} onChange={(e) => setForm({ ...s, approvalRequiredByDefault: e.target.checked })} /> New expenses require approval by default</label>
          <Field label="Approver roles">
            <div className="flex flex-wrap gap-2">
              {roles.map((r) => <button key={r} type="button" onClick={() => toggleRole(r)} className={`rounded-lg px-3 py-1.5 text-xs font-semibold border ${s.approverRoles.includes(r) ? 'bg-brand-500 text-white border-brand-500' : 'border-[var(--line)] text-slate-500'}`}>{r.replace(/([a-z])([A-Z])/g, '$1 $2')}</button>)}
            </div>
          </Field>
          <Field label="Daily (every-evening) summary recipients" hint="Comma-separated emails"><Input value={s.dailySummaryRecipients} onChange={(e) => setForm({ ...s, dailySummaryRecipients: e.target.value })} /></Field>
          <Field label="Weekly summary recipients (HR Director)" hint="Comma-separated emails"><Input value={s.weeklySummaryRecipients} onChange={(e) => setForm({ ...s, weeklySummaryRecipients: e.target.value })} /></Field>
          <Field label="Microsoft Teams webhook URL" hint="Optional — summaries also post to this Teams channel"><Input value={s.teamsWebhookUrl ?? ''} onChange={(e) => setForm({ ...s, teamsWebhookUrl: e.target.value })} placeholder="https://…webhook.office.com/…" /></Field>
        </div>
      )}
    </Modal>
  )
}
