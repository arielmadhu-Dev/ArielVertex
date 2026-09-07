import { useState, useRef } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Receipt, Plus, Settings, Download, Upload, CheckCircle2, XCircle, IndianRupee, Clock, AlertTriangle } from 'lucide-react'
import * as XLSX from 'xlsx'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import type { BillItem, BillSummary, BillSettings, ExtractedBillData } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, StatusPill, Badge, Button, EmptyState, Skeleton } from '../ui/primitives'
import { StatCard } from '../ui/widgets'
import { Modal } from '../ui/Modal'
import { Field, Input, Textarea } from '../ui/form'
import { useToast } from '../ui/Toast'
import { fmtDate } from '../ui/util'
import { P } from '../components/nav'

const inr = (n: number) => '₹' + n.toLocaleString('en-IN')

export default function Bills() {
  const { has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const [open, setOpen] = useState(false)
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [rejecting, setRejecting] = useState<BillItem | null>(null)
  const [reason, setReason] = useState('')
  const [reasonError, setReasonError] = useState('')
  const canManage = has(P.BillsManage)
  const canConfigure = has(P.BillsConfigure)

  const { data, isLoading } = useQuery({
    queryKey: ['bills'],
    queryFn: async () => (await api.get<{ items: BillItem[]; summary: BillSummary }>('/bills')).data,
  })
  const invalidate = () => qc.invalidateQueries({ queryKey: ['bills'] })

  const [form, setForm] = useState({ title: '', category: '', amount: '', vendor: '', billDate: new Date().toISOString().slice(0, 10), dueDate: '', paymentMethod: 'UPI', invoiceNumber: '', approvalRequired: true, description: '' })
  const create = useMutation({
    mutationFn: () => api.post('/bills', { ...form, amount: Number(form.amount), billDate: form.billDate || undefined, dueDate: form.dueDate || undefined }),
    onSuccess: () => { push('Bill created'); setOpen(false); invalidate(); setForm({ title: '', category: '', amount: '', vendor: '', billDate: new Date().toISOString().slice(0, 10), dueDate: '', paymentMethod: 'UPI', invoiceNumber: '', approvalRequired: true, description: '' }) },
    onError: (e) => push(apiError(e), 'error'),
  })
  const act = useMutation({
    mutationFn: ({ id, action, note }: { id: number; action: string; note?: string }) => api.post(`/bills/${id}/${action}`, note !== undefined ? { note } : {}),
    onSuccess: (_d, v) => { push(`Bill ${v.action === 'pay' ? 'marked paid' : v.action + 'd'}`); invalidate() },
    onError: (e) => push(apiError(e), 'error'),
  })

  const uploadInvoice = async (id: number, file: File) => {
    try { const fd = new FormData(); fd.append('file', file); await api.post(`/bills/${id}/invoice`, fd); push('Invoice attached'); invalidate() }
    catch (e) { push(apiError(e), 'error') }
  }
  const download = async (id: number, name?: string) => {
    try { const r = await api.get(`/bills/${id}/invoice/download`, { responseType: 'blob' }); const url = URL.createObjectURL(r.data); const a = document.createElement('a'); a.href = url; a.download = name || 'invoice'; a.click(); URL.revokeObjectURL(url) }
    catch (e) { push(apiError(e), 'error') }
  }

  const s = data?.summary

  const exportExcel = () => {
    const items = data?.items
    if (!items?.length) { push('Nothing to export', 'error'); return }
    const rows = items.map((b) => ({
      'Bill': b.title,
      'Description': b.description,
      'Category': b.category,
      'Amount': b.amount,
      'Bill date': fmtDate(b.billDate),
      'Due date': b.dueDate ? fmtDate(b.dueDate) : '',
      'Vendor': b.vendor,
      'Invoice no.': b.invoiceNumber ?? '',
      'Payment method': b.paymentMethod,
      'Status': b.statusLabel,
      'Approval required': b.approvalRequired ? 'Yes' : 'No',
      'Raised by': b.raisedByName,
      'Approver': b.approverName ?? '',
      'Decision note': b.decisionNote ?? '',
    }))
    const ws = XLSX.utils.json_to_sheet(rows)
    ws['!cols'] = Object.keys(rows[0]).map((k) => ({ wch: Math.max(k.length + 2, ...rows.map((r) => String(r[k as keyof typeof r]).length + 2)) }))
    const wb = XLSX.utils.book_new()
    XLSX.utils.book_append_sheet(wb, ws, 'Bills')
    XLSX.writeFile(wb, `bills-${new Date().toISOString().slice(0, 10)}.xlsx`)
    push('Bill list exported to Excel')
  }

  return (
    <div>
      <PageHeader title="Bills" subtitle="Vendor bills & accounts payable" icon={<Receipt className="h-5 w-5" />}
        actions={<>
          <Button variant="secondary" icon={<Download className="h-4 w-4" />} onClick={exportExcel}>Export Excel</Button>
          {canConfigure && <Button variant="secondary" icon={<Settings className="h-4 w-4" />} onClick={() => setSettingsOpen(true)}>Settings</Button>}
          {canManage && <Button icon={<Plus className="h-4 w-4" />} onClick={() => setOpen(true)}>New bill</Button>}
        </>} />

      <div className="grid grid-cols-2 lg:grid-cols-5 gap-3 mb-4">
        <StatCard label="Bills" value={String(s?.total ?? 0)} t="brand" icon="folder" index={0} />
        <StatCard label="Awaiting approval" value={String(s?.pendingApproval ?? 0)} t={s?.pendingApproval ? 'warn' : 'good'} icon="clipboard" index={1} />
        <StatCard label="Overdue" value={String(s?.overdue ?? 0)} t={s?.overdue ? 'danger' : 'good'} icon="alert" index={2} />
        <StatCard label="Paid" value={String(s?.paid ?? 0)} t="good" icon="check" index={3} />
        <StatCard label="Total value" value={inr(s?.totalAmount ?? 0)} t="info" icon="trending" index={4} />
      </div>

      <Card className="overflow-hidden">
        <div className="overflow-x-auto av-scroll-x">
          <table className="w-full text-sm">
            <thead><tr className="text-left text-xs uppercase tracking-wide text-slate-400 border-b border-[var(--line)]">
              <th className="px-5 py-3">Bill</th><th className="px-5 py-3">Category</th><th className="px-5 py-3 text-right">Amount</th>
              <th className="px-5 py-3">Due Date</th><th className="px-5 py-3">Status</th><th className="px-5 py-3">Invoice</th><th className="px-5 py-3">Raised by</th><th className="px-5 py-3 text-right">Actions</th>
            </tr></thead>
            <tbody>
              {isLoading ? [...Array(4)].map((_, i) => <tr key={i}><td colSpan={8} className="px-5 py-2"><Skeleton className="h-9" /></td></tr>)
                : data?.items.length ? data.items.map((b) => {
                  const isOverdue = b.status === 'Overdue'
                  const isDueSoon = !isOverdue && b.daysUntilDue >= 0 && b.daysUntilDue <= 3
                  return (
                    <tr key={b.id} className={`border-b border-[var(--line)] last:border-0 hover:bg-slate-50 dark:hover:bg-navy-600 ${isOverdue ? 'bg-red-50 dark:bg-red-950/20' : isDueSoon ? 'bg-amber-50 dark:bg-amber-950/20' : ''}`}>
                      <td className="px-5 py-3">
                        <p className="font-semibold">{b.title}</p>
                        <p className="text-xs text-slate-400">{b.vendor || '—'} · {fmtDate(b.billDate)}{(b.approvalRequired && (b.status === 'Submitted' || b.status === 'Overdue')) && <span className="ml-1 text-amber-500">· approval required</span>}</p>
                      </td>
                      <td className="px-5 py-3"><Badge t="info">{b.category}</Badge></td>
                      <td className="px-5 py-3 text-right font-bold whitespace-nowrap tabular-nums">{inr(b.amount)}</td>
                      <td className="px-5 py-3">
                        {b.dueDate ? (
                          <span className={`inline-flex items-center gap-1 text-xs font-semibold ${isOverdue ? 'text-red-600' : isDueSoon ? 'text-amber-600' : 'text-slate-500'}`}>
                            {isOverdue && <AlertTriangle className="h-3.5 w-3.5" />}
                            {!isOverdue && isDueSoon && <Clock className="h-3.5 w-3.5" />}
                            {fmtDate(b.dueDate)}{isOverdue ? ' (overdue)' : isDueSoon ? ` (${b.daysUntilDue}d)` : ''}
                          </span>
                        ) : <span className="text-xs text-slate-400">—</span>}
                      </td>
                      <td className="px-5 py-3"><StatusPill value={b.status} /></td>
                      <td className="px-5 py-3">
                        {b.hasInvoiceFile
                          ? <button onClick={() => download(b.id, b.invoiceFileName)} className="inline-flex items-center gap-1 text-brand-500 hover:text-brand-600 text-xs font-semibold"><Download className="h-3.5 w-3.5" />{b.invoiceNumber || 'file'}</button>
                          : b.canManage
                            ? <label className="inline-flex items-center gap-1 text-slate-400 hover:text-brand-500 text-xs font-semibold cursor-pointer"><Upload className="h-3.5 w-3.5" />Attach<input type="file" className="hidden" onChange={(ev) => ev.target.files?.[0] && uploadInvoice(b.id, ev.target.files[0])} /></label>
                            : <span className="text-xs text-slate-400">{b.invoiceNumber || '—'}</span>}
                      </td>
                      <td className="px-5 py-3 text-slate-500">{b.raisedByName}</td>
                      <td className="px-5 py-3">
                        <div className="flex items-center gap-1.5 justify-end">
                          {b.canApprove && (b.status === 'Submitted' || b.status === 'Overdue' || (b.approvalRequired && b.status === 'Draft')) && <>
                            <Button size="sm" variant="secondary" icon={<CheckCircle2 className="h-4 w-4" />} onClick={() => act.mutate({ id: b.id, action: 'approve', note: 'Approved' })}>Approve</Button>
                            <Button size="sm" variant="ghost" icon={<XCircle className="h-4 w-4" />} onClick={() => { setReason(''); setReasonError(''); setRejecting(b) }}>Reject</Button>
                          </>}
                          {b.canManage && b.status !== 'Paid' && b.status !== 'Rejected' && (!b.approvalRequired || b.status === 'Approved') &&
                            <Button size="sm" icon={<IndianRupee className="h-4 w-4" />} onClick={() => act.mutate({ id: b.id, action: 'pay' })}>Mark paid</Button>}
                        </div>
                      </td>
                    </tr>
                  )
                }) : <tr><td colSpan={8}><EmptyState icon={<Receipt className="h-6 w-6" />} title="No bills yet" hint="Add a vendor bill to get started." /></td></tr>}
            </tbody>
          </table>
        </div>
      </Card>

      {/* New bill */}
      <Modal open={open} onClose={() => setOpen(false)} title="New bill"
        footer={<><Button variant="secondary" onClick={() => setOpen(false)}>Cancel</Button><Button loading={create.isPending} disabled={!form.title || !form.amount || !form.category} onClick={() => create.mutate()}>Create bill</Button></>}>
        <div className="space-y-4">
          <AiUploadArea onExtracted={(d) => {
            setForm((prev) => ({ ...prev,
              vendor: d.vendor ?? prev.vendor,
              amount: d.amount ? String(d.amount) : prev.amount,
              dueDate: d.dueDate ?? prev.dueDate,
              billDate: d.billDate ?? prev.billDate,
              category: d.category ?? prev.category,
              invoiceNumber: d.invoiceNumber ?? prev.invoiceNumber,
            }))
            push('Bill data extracted! Review and save.')
          }} push={push} />
          <div className="grid sm:grid-cols-2 gap-4">
            <div className="sm:col-span-2"><Field label="Title" required><Input value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} placeholder="e.g. Office rent" /></Field></div>
            <Field label="Category" required><Input value={form.category} onChange={(e) => setForm({ ...form, category: e.target.value })} placeholder="e.g. Office Rent, Software, Utilities" /></Field>
            <Field label="Amount (₹)" required><Input type="number" min={0} value={form.amount} onChange={(e) => setForm({ ...form, amount: e.target.value })} /></Field>
            <Field label="Vendor / supplier"><Input value={form.vendor} onChange={(e) => setForm({ ...form, vendor: e.target.value })} /></Field>
            <Field label="Invoice number"><Input value={form.invoiceNumber} onChange={(e) => setForm({ ...form, invoiceNumber: e.target.value })} /></Field>
            <Field label="Bill date"><Input type="date" value={form.billDate} onChange={(e) => setForm({ ...form, billDate: e.target.value })} /></Field>
            <Field label="Due date"><Input type="date" value={form.dueDate} onChange={(e) => setForm({ ...form, dueDate: e.target.value })} /></Field>
            <Field label="Payment method"><Input value={form.paymentMethod} onChange={(e) => setForm({ ...form, paymentMethod: e.target.value })} placeholder="UPI / Card / Bank" /></Field>
            <div className="sm:col-span-2"><Field label="Notes"><Textarea value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></Field></div>
            <div className="sm:col-span-2"><label className="flex items-center gap-2 text-sm font-medium"><input type="checkbox" checked={form.approvalRequired} onChange={(e) => setForm({ ...form, approvalRequired: e.target.checked })} /> Requires approval before payment</label></div>
          </div>
        </div>
      </Modal>

      {/* Reject bill */}
      <Modal open={!!rejecting} onClose={() => setRejecting(null)} title="Reject bill" subtitle={rejecting?.title} size="sm"
        footer={<>
          <Button variant="secondary" onClick={() => setRejecting(null)}>Cancel</Button>
          <Button variant="danger" icon={<XCircle className="h-4 w-4" />} loading={act.isPending}
            onClick={() => {
              const r = reason.trim()
              if (!r) { setReasonError('Please provide a reason before rejecting.'); return }
              act.mutate({ id: rejecting!.id, action: 'reject', note: r })
              setRejecting(null)
            }}>Confirm rejection</Button>
        </>}>
        <div className="space-y-4">
          <Field label="Reason for rejection" required error={reasonError}>
            <Textarea rows={4} value={reason} onChange={(e) => { setReason(e.target.value); if (reasonError) setReasonError('') }}
              placeholder="Why are you rejecting this bill?" autoFocus />
          </Field>
        </div>
      </Modal>

      {settingsOpen && <SettingsModal onClose={() => setSettingsOpen(false)} push={push} />}
    </div>
  )
}

function AiUploadArea({ onExtracted, push }: { onExtracted: (d: ExtractedBillData) => void; push: (m: string, k?: any) => void }) {
  const fileRef = useRef<HTMLInputElement>(null)
  const [extracting, setExtracting] = useState(false)
  const [extracted, setExtracted] = useState(false)

  const handleFile = async (file: File) => {
    setExtracting(true)
    setExtracted(false)
    try {
      const fd = new FormData()
      fd.append('file', file)
      const r = await api.post<ExtractedBillData>('/bills/extract', fd)
      onExtracted(r.data)
      setExtracted(true)
    } catch (e) {
      push(apiError(e), 'error')
    } finally {
      setExtracting(false)
    }
  }

  return (
    <div
      className={`border-2 border-dashed rounded-lg p-6 text-center cursor-pointer transition-colors ${extracting ? 'border-brand-400 bg-brand-50 dark:bg-brand-950/20' : extracted ? 'border-green-400 bg-green-50 dark:bg-green-950/20' : 'border-slate-300 hover:border-brand-400'}`}
      onClick={() => !extracting && fileRef.current?.click()}
    >
      <input ref={fileRef} type="file" accept=".pdf,.png,.jpg,.jpeg,.gif" className="hidden" onChange={(e) => e.target.files?.[0] && handleFile(e.target.files[0])} />
      {extracting ? (
        <div className="flex flex-col items-center gap-2">
          <div className="h-6 w-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
          <p className="text-sm font-medium text-brand-600">Extracting with AI...</p>
        </div>
      ) : extracted ? (
        <div className="flex flex-col items-center gap-2">
          <CheckCircle2 className="h-6 w-6 text-green-500" />
          <p className="text-sm font-medium text-green-600">Extracted! Review fields below.</p>
        </div>
      ) : (
        <div className="flex flex-col items-center gap-2">
          <Upload className="h-6 w-6 text-slate-400" />
          <p className="text-sm font-medium text-slate-600">Drop bill here or click to upload</p>
          <p className="text-xs text-slate-400">PDF, PNG, JPG — AI will extract the data</p>
        </div>
      )}
    </div>
  )
}

function SettingsModal({ onClose, push }: { onClose: () => void; push: (m: string, k?: any) => void }) {
  const qc = useQueryClient()
  const { data } = useQuery({ queryKey: ['bill-settings'], queryFn: async () => (await api.get<BillSettings>('/bill-settings')).data })
  const [form, setForm] = useState<BillSettings | null>(null)
  const s = form ?? data
  const roles = ['HrDirector', 'Accountant', 'CeoAdmin']
  const toggleRole = (r: string) => { if (!s) return; const has = s.approverRoles.includes(r); setForm({ ...s, approverRoles: has ? s.approverRoles.filter((x) => x !== r) : [...s.approverRoles, r] }) }
  const save = useMutation({
    mutationFn: () => api.put('/bill-settings', s),
    onSuccess: () => { push('Settings saved'); qc.invalidateQueries({ queryKey: ['bill-settings'] }); onClose() },
    onError: (e) => push(apiError(e), 'error'),
  })
  return (
    <Modal open onClose={onClose} title="Bill settings" subtitle="Configure approval, reminders & summaries"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={save.isPending} disabled={!s} onClick={() => save.mutate()}>Save</Button></>}>
      {!s ? <Skeleton className="h-40" /> : (
        <div className="space-y-4">
          <label className="flex items-center gap-2 text-sm font-medium"><input type="checkbox" checked={s.approvalRequiredByDefault} onChange={(e) => setForm({ ...s, approvalRequiredByDefault: e.target.checked })} /> New bills require approval by default</label>
          <Field label="Approver roles">
            <div className="flex flex-wrap gap-2">
              {roles.map((r) => <button key={r} type="button" onClick={() => toggleRole(r)} className={`rounded-lg px-3 py-1.5 text-xs font-semibold border ${s.approverRoles.includes(r) ? 'bg-brand-500 text-white border-brand-500' : 'border-[var(--line)] text-slate-500'}`}>{r.replace(/([a-z])([A-Z])/g, '$1 $2')}</button>)}
            </div>
          </Field>
          <Field label="Reminder days before due" hint="Comma-separated (e.g. 7,3,1)"><Input value={s.reminderDaysBefore} onChange={(e) => setForm({ ...s, reminderDaysBefore: e.target.value })} /></Field>
          <Field label="Reminder recipients" hint="Comma-separated emails"><Input value={s.reminderRecipients} onChange={(e) => setForm({ ...s, reminderRecipients: e.target.value })} /></Field>
          <Field label="Daily (every-evening) summary recipients" hint="Comma-separated emails"><Input value={s.dailySummaryRecipients} onChange={(e) => setForm({ ...s, dailySummaryRecipients: e.target.value })} /></Field>
          <Field label="Weekly summary recipients" hint="Comma-separated emails"><Input value={s.weeklySummaryRecipients} onChange={(e) => setForm({ ...s, weeklySummaryRecipients: e.target.value })} /></Field>
          <Field label="Microsoft Teams webhook URL" hint="Optional — summaries + reminders also post to this Teams channel"><Input value={s.teamsWebhookUrl ?? ''} onChange={(e) => setForm({ ...s, teamsWebhookUrl: e.target.value })} placeholder="https://…webhook.office.com/…" /></Field>
        </div>
      )}
    </Modal>
  )
}
