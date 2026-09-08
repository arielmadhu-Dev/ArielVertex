import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Banknote, Plus, Trash2, Pencil, Download } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import type { PettyCashItem } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, StatusPill, Button, EmptyState, Skeleton } from '../ui/primitives'
import { StatCard } from '../ui/widgets'
import { Modal } from '../ui/Modal'
import { Field, Input } from '../ui/form'
import { useToast } from '../ui/Toast'
import { fmtDate } from '../ui/util'
import { P } from '../components/nav'

const inr = (n: number) => '₹' + n.toLocaleString('en-IN')

export default function PettyCash() {
  const { has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const canManage = has(P.PettyCashManage)
  const canView = has(P.PettyCashViewAll) || canManage
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<PettyCashItem | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['petty-cash'],
    queryFn: async () => (await api.get<PettyCashItem[]>('/petty-cash')).data,
    enabled: canView
  })
  const invalidate = () => qc.invalidateQueries({ queryKey: ['petty-cash'] })

  const exportCsv = async () => {
    try {
      const res = await api.get('/petty-cash/export', { responseType: 'blob' })
      const url = URL.createObjectURL(res.data as Blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `petty-cash-${new Date().toISOString().slice(0, 10)}.csv`
      a.click()
      URL.revokeObjectURL(url)
      push('CSV exported')
    } catch (e) { push(apiError(e), 'error') }
  }

  const [form, setForm] = useState({ date: new Date().toISOString().slice(0, 10), particulars: '', credit: '', debit: '', notes: '' })
  const create = useMutation({
    mutationFn: () => api.post('/petty-cash', { ...form, credit: Number(form.credit) || 0, debit: Number(form.debit) || 0 }),
    onSuccess: () => { push('Entry added'); setOpen(false); invalidate(); setForm({ date: new Date().toISOString().slice(0, 10), particulars: '', credit: '', debit: '', notes: '' }) },
    onError: (e) => push(apiError(e), 'error'),
  })

  const update = useMutation({
    mutationFn: () => api.put(`/petty-cash/${editing!.id}`, { ...form, credit: Number(form.credit) || 0, debit: Number(form.debit) || 0 }),
    onSuccess: () => { push('Entry updated'); setEditing(null); invalidate(); setForm({ date: new Date().toISOString().slice(0, 10), particulars: '', credit: '', debit: '', notes: '' }) },
    onError: (e) => push(apiError(e), 'error'),
  })

  const remove = useMutation({
    mutationFn: (id: number) => api.delete(`/petty-cash/${id}`),
    onSuccess: () => { push('Entry deleted'); invalidate() },
    onError: (e) => push(apiError(e), 'error'),
  })

  const openEdit = (item: PettyCashItem) => {
    setEditing(item)
    setForm({ date: item.date, particulars: item.particulars, credit: String(item.credit), debit: String(item.debit), notes: item.notes ?? '' })
  }

  const items = data ?? []
  const totalCredit = items.reduce((s, i) => s + i.credit, 0)
  const totalDebit = items.reduce((s, i) => s + i.debit, 0)
  const currentBalance = items.length > 0 ? items[items.length - 1].balance : 0

  return (
    <div>
      <PageHeader title="Petty Cash" subtitle="Cash book ledger — track petty cash inflows and outflows" icon={<Banknote className="h-5 w-5" />}
        actions={
          <div className="flex gap-2">
            <Button variant="secondary" icon={<Download className="h-4 w-4" />} onClick={exportCsv}>Export CSV</Button>
            {canManage ? <Button icon={<Plus className="h-4 w-4" />} onClick={() => setOpen(true)}>Add Entry</Button> : undefined}
          </div>
        } />

      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 mb-4">
        <StatCard label="Entries" value={String(items.length)} t="brand" icon="folder" index={0} />
        <StatCard label="Total Credit" value={inr(totalCredit)} t="good" icon="trending" index={1} />
        <StatCard label="Total Debit" value={inr(totalDebit)} t="warn" icon="trending" index={2} />
        <StatCard label="Current Balance" value={inr(currentBalance)} t="info" icon="wallet" index={3} />
      </div>

      <Card className="overflow-hidden">
        <div className="overflow-x-auto av-scroll-x">
          <table className="w-full text-sm">
            <thead><tr className="text-left text-xs uppercase tracking-wide text-slate-400 border-b border-[var(--line)]">
              <th className="px-5 py-3">SL No.</th>
              <th className="px-5 py-3">Date</th>
              <th className="px-5 py-3">Particulars</th>
              <th className="px-5 py-3 text-right">Opening Balance</th>
              <th className="px-5 py-3 text-right">Credit</th>
              <th className="px-5 py-3 text-right">Debit</th>
              <th className="px-5 py-3 text-right">Balance</th>
              <th className="px-5 py-3">Status</th>
              {canManage && <th className="px-5 py-3 text-right">Actions</th>}
            </tr></thead>
            <tbody>
              {isLoading ? [...Array(4)].map((_, i) => <tr key={i}><td colSpan={canManage ? 9 : 8} className="px-5 py-2"><Skeleton className="h-9" /></td></tr>)
                : items.length ? items.map((item) => (
                  <tr key={item.id} className="border-b border-[var(--line)] last:border-0 hover:bg-slate-50 dark:hover:bg-navy-600">
                    <td className="px-5 py-3 text-slate-500">{item.id}</td>
                    <td className="px-5 py-3">{fmtDate(item.date)}</td>
                    <td className="px-5 py-3">
                      <p className="font-medium">{item.particulars}</p>
                      {item.notes && <p className="text-xs text-slate-400">{item.notes}</p>}
                    </td>
                    <td className="px-5 py-3 text-right tabular-nums">{inr(item.openingBalance)}</td>
                    <td className="px-5 py-3 text-right tabular-nums text-green-600">{item.credit > 0 ? `+${inr(item.credit)}` : '—'}</td>
                    <td className="px-5 py-3 text-right tabular-nums text-red-600">{item.debit > 0 ? `-${inr(item.debit)}` : '—'}</td>
                    <td className="px-5 py-3 text-right font-bold tabular-nums">{inr(item.balance)}</td>
                    <td className="px-5 py-3"><StatusPill value={item.status} /></td>
                    {canManage && (
                      <td className="px-5 py-3 text-right">
                        <Button size="sm" variant="ghost" icon={<Pencil className="h-4 w-4" />} onClick={() => openEdit(item)} />
                        <Button size="sm" variant="ghost" icon={<Trash2 className="h-4 w-4" />} onClick={() => { if (window.confirm('Delete this entry?')) remove.mutate(item.id) }} />
                      </td>
                    )}
                  </tr>
                )) : <tr><td colSpan={canManage ? 9 : 8}><EmptyState icon={<Banknote className="h-6 w-6" />} title="No entries" hint={canManage ? 'Add your first petty cash entry.' : 'No petty cash entries yet.'} /></td></tr>}
            </tbody>
          </table>
        </div>
      </Card>

      {open && (
        <Modal open={open} onClose={() => setOpen(false)} title="Add Petty Cash Entry"
          footer={<><Button variant="secondary" onClick={() => setOpen(false)}>Cancel</Button><Button loading={create.isPending} disabled={!form.particulars || (!form.credit && !form.debit)} onClick={() => create.mutate()}>Add Entry</Button></>}>
          <div className="grid sm:grid-cols-2 gap-4">
            <Field label="Date" required><Input type="date" value={form.date} onChange={(e) => setForm({ ...form, date: e.target.value })} /></Field>
            <Field label="Particulars" required><Input value={form.particulars} onChange={(e) => setForm({ ...form, particulars: e.target.value })} placeholder="e.g. Office stationery" /></Field>
            <Field label="Credit (Inflow)"><Input type="number" min={0} value={form.credit} onChange={(e) => setForm({ ...form, credit: e.target.value })} /></Field>
            <Field label="Debit (Outflow)"><Input type="number" min={0} value={form.debit} onChange={(e) => setForm({ ...form, debit: e.target.value })} /></Field>
            <div className="sm:col-span-2"><Field label="Notes"><Input value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} /></Field></div>
          </div>
        </Modal>
      )}

      {editing && (
        <Modal open={!!editing} onClose={() => setEditing(null)} title="Edit Petty Cash Entry"
          footer={<><Button variant="secondary" onClick={() => setEditing(null)}>Cancel</Button><Button loading={update.isPending} disabled={!form.particulars || (!form.credit && !form.debit)} onClick={() => update.mutate()}>Save changes</Button></>}>
          <div className="grid sm:grid-cols-2 gap-4">
            <Field label="Date" required><Input type="date" value={form.date} onChange={(e) => setForm({ ...form, date: e.target.value })} /></Field>
            <Field label="Particulars" required><Input value={form.particulars} onChange={(e) => setForm({ ...form, particulars: e.target.value })} placeholder="e.g. Office stationery" /></Field>
            <Field label="Credit (Inflow)"><Input type="number" min={0} value={form.credit} onChange={(e) => setForm({ ...form, credit: e.target.value })} /></Field>
            <Field label="Debit (Outflow)"><Input type="number" min={0} value={form.debit} onChange={(e) => setForm({ ...form, debit: e.target.value })} /></Field>
            <div className="sm:col-span-2"><Field label="Notes"><Input value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} /></Field></div>
          </div>
        </Modal>
      )}
    </div>
  )
}
