import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Package, Plus, Laptop, Wrench, ShoppingCart, UserPlus, CheckCircle2, XCircle, Monitor, Keyboard, Mouse, Printer, Network, Sofa, PrinterIcon } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import { useEnums } from '../lib/hooks'
import type { Asset, AssetRequest, UserListItem, Paged } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, StatusPill, Button, EmptyState, Skeleton, Badge } from '../ui/primitives'
import { StatCard } from '../ui/widgets'
import { Modal } from '../ui/Modal'
import { Field, Input, Select, Textarea } from '../ui/form'
import { useToast } from '../ui/Toast'
import { fmtDate } from '../ui/util'
import { P } from '../components/nav'

const REQUEST_TYPES = [
  { value: 'New', label: 'New Purchase' },
  { value: 'Repair', label: 'Repair' },
  { value: 'Replacement', label: 'Replacement' },
]

const ASSET_ICONS: Record<string, any> = {
  Laptop: Laptop, Desktop: Monitor, Monitor: Monitor, Keyboard: Keyboard, Mouse: Mouse,
  Printer: PrinterIcon, NetworkDevice: Network, Software: Monitor, Furniture: Sofa, Other: Package
}

export default function Assets() {
  const { has, user } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const enums = useEnums()
  const canManage = has(P.AssetsManage)
  const canApprove = has(P.AssetsApprove)
  const canRaise = has(P.AssetsRaise)
  const myId = user?.id ?? 0

  const [tab, setTab] = useState<'assets' | 'requests'>('assets')
  const [assetOpen, setAssetOpen] = useState(false)
  const [requestOpen, setRequestOpen] = useState(false)
  const [rejecting, setRejecting] = useState<AssetRequest | null>(null)
  const [reason, setReason] = useState('')
  const [reasonError, setReasonError] = useState('')

  const { data: assets, isLoading: assetsLoading } = useQuery({
    queryKey: ['assets'],
    queryFn: async () => (await api.get<Asset[]>('/assets')).data,
    enabled: canManage || canRaise
  })
  const invalidateAssets = () => qc.invalidateQueries({ queryKey: ['assets'] })

  const { data: requests, isLoading: requestsLoading } = useQuery({
    queryKey: ['asset-requests'],
    queryFn: async () => (await api.get<AssetRequest[]>('/asset-requests')).data,
    enabled: canManage || canApprove || canRaise
  })
  const invalidateRequests = () => qc.invalidateQueries({ queryKey: ['asset-requests'] })

  const { data: users } = useQuery({
    queryKey: ['users-list'],
    queryFn: async () => (await api.get<Paged<UserListItem>>('/employees', { params: { pageSize: 100 } })).data.items,
    enabled: canManage
  })

  const [assigning, setAssigning] = useState<Asset | null>(null)
  const [assignUserId, setAssignUserId] = useState('')

  const returnAsset = useMutation({
    mutationFn: (id: number) => api.put(`/assets/${id}`, { status: 'Available', assignedToId: null }),
    onSuccess: () => { push('Asset returned'); invalidateAssets() },
    onError: (e) => push(apiError(e), 'error'),
  })

  const requestAssignment = useMutation({
    mutationFn: ({ assetId, targetUserId, subject }: { assetId: number; targetUserId: number; subject: string }) =>
      api.post('/asset-requests', { subject, description: 'Asset assignment request', requestType: 'Assignment', assetId, targetUserId, quantity: 1 }),
    onSuccess: () => { push('Assignment request sent to employee'); setAssigning(null); setAssignUserId(''); invalidateRequests(); invalidateAssets() },
    onError: (e) => push(apiError(e), 'error'),
  })

  const [assetForm, setAssetForm] = useState({ name: '', description: '', category: 'Laptop', serialNumber: '', purchaseDate: '', purchasePrice: '', condition: 'New' })
  const createAsset = useMutation({
    mutationFn: () => {
      const payload = { ...assetForm, purchasePrice: assetForm.purchasePrice ? Number(assetForm.purchasePrice) : null };
      console.log('[Assets] Creating asset:', payload);
      return api.post('/assets', payload);
    },
    onSuccess: () => { push('Asset created'); setAssetOpen(false); invalidateAssets(); setAssetForm({ name: '', description: '', category: 'Laptop', serialNumber: '', purchaseDate: '', purchasePrice: '', condition: 'New' }) },
    onError: (e) => push(apiError(e), 'error'),
  })

  const [requestForm, setRequestForm] = useState({ subject: '', description: '', requestType: 'New', assetId: '', quantity: '1', estimatedCost: '', vendor: '' })
  const createRequest = useMutation({
    mutationFn: () => {
      const payload = { ...requestForm, assetId: requestForm.assetId ? Number(requestForm.assetId) : null, quantity: Number(requestForm.quantity) || 1, estimatedCost: requestForm.estimatedCost ? Number(requestForm.estimatedCost) : null };
      console.log('[Assets] Creating request:', payload);
      return api.post('/asset-requests', payload);
    },
    onSuccess: () => { push('Request raised'); setRequestOpen(false); invalidateRequests(); setRequestForm({ subject: '', description: '', requestType: 'New', assetId: '', quantity: '1', estimatedCost: '', vendor: '' }) },
    onError: (e) => push(apiError(e), 'error'),
  })

  const act = useMutation({
    mutationFn: ({ id, action, note }: { id: number; action: string; note?: string }) => api.post(`/asset-requests/${id}/${action}`, note !== undefined ? { note } : {}),
    onSuccess: (_d, v) => {
      const label = v.action === 'complete' ? 'completed' : v.action === 'accept' ? 'accepted' : v.action === 'decline' ? 'declined' : v.action + 'd'
      push(`Request ${label}`); invalidateRequests(); invalidateAssets()
    },
    onError: (e) => push(apiError(e), 'error'),
  })

  const assetList = assets ?? []
  const requestList = requests ?? []
  const totalAssets = assetList.length
  const assignedAssets = assetList.filter(a => a.status === 'Assigned').length
  const availableAssets = assetList.filter(a => a.status === 'Available').length
  const pendingRequests = requestList.filter(r => r.status === 'Pending' || r.status === 'PendingAcceptance').length

  const procurementRequests = requestList.filter(r => r.requestType === 'New' || r.requestType === 'Repair' || r.requestType === 'Replacement')
  const assignmentRequests = requestList.filter(r => r.requestType === 'Assignment')
  const pendingAssignments = assignmentRequests.filter(r => r.targetUserId === myId && (r.status === 'Pending' || r.status === 'PendingAcceptance'))
  const showRequestsTab = canManage || canApprove

  const AssetIcon = ({ category }: { category: string }) => {
    const Icon = ASSET_ICONS[category] || Package
    return <Icon className="h-5 w-5 text-slate-400" />
  }

  return (
    <div className="space-y-6">
      <PageHeader title="Asset Management" subtitle="Manage company assets, assignments, and requests" icon={<Package className="h-5 w-5" />} />

      {/* Tab Navigation */}
      <div className="border-b border-[var(--line)]">
        <nav className="flex gap-1">
          <button
            onClick={() => setTab('assets')}
            className={`px-4 py-2.5 text-sm font-medium border-b-2 transition-colors ${
              tab === 'assets'
                ? 'border-brand-500 text-brand-600'
                : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
            }`}
          >
            <span className="flex items-center gap-2">
              <Laptop className="h-4 w-4" />
              Assets
              <span className={`text-xs px-2 py-0.5 rounded-full ${tab === 'assets' ? 'bg-brand-100 text-brand-700' : 'bg-slate-100 text-slate-500'}`}>
                {totalAssets}
              </span>
            </span>
          </button>
          {showRequestsTab && (
          <button
            onClick={() => setTab('requests')}
            className={`px-4 py-2.5 text-sm font-medium border-b-2 transition-colors ${
              tab === 'requests'
                ? 'border-brand-500 text-brand-600'
                : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
            }`}
          >
            <span className="flex items-center gap-2">
              <Wrench className="h-4 w-4" />
              Requests
              {pendingRequests > 0 && (
                <span className="text-xs px-2 py-0.5 rounded-full bg-amber-100 text-amber-700">
                  {pendingRequests} pending
                </span>
              )}
            </span>
          </button>
          )}
        </nav>
      </div>

      {/* Assets Tab */}
      {tab === 'assets' && (
        <div className="space-y-4">
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
            <StatCard label="Total Assets" value={String(totalAssets)} t="brand" icon="folder" index={0} />
            <StatCard label="Assigned" value={String(assignedAssets)} t="warn" icon="users" index={1} />
            <StatCard label="Available" value={String(availableAssets)} t="good" icon="check" index={2} />
            <StatCard label="Pending Requests" value={String(pendingRequests)} t="info" icon="clipboard" index={3} />
          </div>

          {canManage && (
            <div className="flex justify-end">
              <Button icon={<Plus className="h-4 w-4" />} onClick={() => setAssetOpen(true)}>
                Add Asset
              </Button>
            </div>
          )}

          <Card className="overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-xs uppercase tracking-wide text-slate-400 border-b border-[var(--line)]">
                    <th className="px-5 py-3">Asset</th>
                    <th className="px-5 py-3">Category</th>
                    <th className="px-5 py-3">Serial Number</th>
                    <th className="px-5 py-3">Condition</th>
                    <th className="px-5 py-3">Status</th>
                    <th className="px-5 py-3">Assigned To</th>
                    <th className="px-5 py-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {assetsLoading ? (
                    [...Array(4)].map((_, i) => (
                      <tr key={i}>
                        <td colSpan={7} className="px-5 py-2">
                          <Skeleton className="h-9" />
                        </td>
                      </tr>
                    ))
                  ) : assetList.length ? assetList.map((a) => {
                      const pendingAssignment = pendingAssignments.find((p) => p.assetId === a.id)
                      return (
                    <tr key={a.id} className="border-b border-[var(--line)] last:border-0 hover:bg-slate-50 dark:hover:bg-navy-600 transition-colors">
                      <td className="px-5 py-3">
                        <div className="flex items-center gap-3">
                          <div className="p-2 bg-slate-100 dark:bg-navy-700 rounded-lg">
                            <AssetIcon category={a.category} />
                          </div>
                          <div>
                            <p className="font-semibold text-slate-900 dark:text-slate-100">{a.name}</p>
                            <p className="text-xs text-slate-500 dark:text-slate-400">{a.description}</p>
                          </div>
                        </div>
                      </td>
                      <td className="px-5 py-3">
                        <Badge t="info">{a.categoryLabel}</Badge>
                      </td>
                      <td className="px-5 py-3 text-xs text-slate-500 dark:text-slate-400 font-mono">
                        {a.serialNumber || '—'}
                      </td>
                      <td className="px-5 py-3 text-xs text-slate-600 dark:text-slate-300">
                        {a.conditionLabel}
                      </td>
                      <td className="px-5 py-3">
                        <StatusPill value={a.status} />
                      </td>
                      <td className="px-5 py-3 text-xs text-slate-600 dark:text-slate-300">
                        {a.assignedToName || '—'}
                      </td>
                      <td className="px-5 py-3 text-right">
                        <div className="flex items-center gap-2 justify-end">
                          {pendingAssignment && <>
                            <Button
                              size="sm"
                              variant="secondary"
                              icon={<CheckCircle2 className="h-4 w-4" />}
                              onClick={() => act.mutate({ id: pendingAssignment.id, action: 'accept' })}
                            >
                              Accept
                            </Button>
                            <Button
                              size="sm"
                              variant="ghost"
                              icon={<XCircle className="h-4 w-4" />}
                              onClick={() => act.mutate({ id: pendingAssignment.id, action: 'decline' })}
                            >
                              Decline
                            </Button>
                          </>}
                          {!pendingAssignment && a.status === 'Available' && canManage && (
                            <Button
                              size="sm"
                              variant="secondary"
                              icon={<UserPlus className="h-4 w-4" />}
                              onClick={() => { setAssigning(a); setAssignUserId('') }}
                            >
                              Assign
                            </Button>
                          )}
                          {!pendingAssignment && a.status === 'Assigned' && canManage && (
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() => returnAsset.mutate(a.id)}
                            >
                              Return
                            </Button>
                          )}
                        </div>
                      </td>
                    </tr>
                      )
                    }) : (
                    <tr>
                      <td colSpan={7}>
                        <EmptyState
                          icon={<Package className="h-6 w-6" />}
                          title="No assets"
                          hint={canManage ? 'Add your first asset to get started.' : 'No assets available yet.'}
                        />
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </Card>
        </div>
      )}

      {/* Requests Tab */}
      {tab === 'requests' && (
        <div className="space-y-4">
          {canManage && (
            <div className="flex justify-end">
              <Button icon={<ShoppingCart className="h-4 w-4" />} onClick={() => setRequestOpen(true)}>
                Request New Asset
              </Button>
            </div>
          )}

          {(canManage || canApprove) && (
          <Card className="overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-xs uppercase tracking-wide text-slate-400 border-b border-[var(--line)]">
                    <th className="px-5 py-3">Request</th>
                    <th className="px-5 py-3">Type</th>
                    <th className="px-5 py-3">Asset</th>
                    <th className="px-5 py-3">Qty</th>
                    <th className="px-5 py-3">Cost</th>
                    <th className="px-5 py-3">Status</th>
                    <th className="px-5 py-3">Raised By</th>
                    <th className="px-5 py-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {requestsLoading ? (
                    [...Array(4)].map((_, i) => (
                      <tr key={i}>
                        <td colSpan={8} className="px-5 py-2">
                          <Skeleton className="h-9" />
                        </td>
                      </tr>
                    ))
                  ) : procurementRequests.length ? procurementRequests.map((r) => (
                    <tr key={r.id} className="border-b border-[var(--line)] last:border-0 hover:bg-slate-50 dark:hover:bg-navy-600 transition-colors">
                      <td className="px-5 py-3">
                        <p className="font-semibold text-slate-900 dark:text-slate-100">{r.subject}</p>
                        <p className="text-xs text-slate-500 dark:text-slate-400 line-clamp-1">{r.description}</p>
                      </td>
                      <td className="px-5 py-3">
                        <Badge t="info">{r.requestTypeLabel}</Badge>
                      </td>
                      <td className="px-5 py-3 text-xs text-slate-600 dark:text-slate-300">
                        {r.assetName || '—'}
                      </td>
                      <td className="px-5 py-3 text-xs text-slate-600 dark:text-slate-300">
                        {r.quantity}
                      </td>
                      <td className="px-5 py-3 text-xs text-slate-600 dark:text-slate-300">
                        {r.estimatedCost ? `₹${r.estimatedCost.toLocaleString()}` : '—'}
                      </td>
                      <td className="px-5 py-3">
                        <StatusPill value={r.status} />
                      </td>
                      <td className="px-5 py-3 text-xs text-slate-600 dark:text-slate-300">
                        {r.raisedByName}
                      </td>
                      <td className="px-5 py-3 text-right">
                        <div className="flex items-center gap-2 justify-end">
                          {r.status === 'Pending' && canApprove && <>
                            <Button
                              size="sm"
                              variant="secondary"
                              icon={<CheckCircle2 className="h-4 w-4" />}
                              onClick={() => act.mutate({ id: r.id, action: 'approve', note: 'Approved' })}
                            >
                              Approve
                            </Button>
                            <Button
                              size="sm"
                              variant="ghost"
                              icon={<XCircle className="h-4 w-4" />}
                              onClick={() => { setRejecting(r); setReason(''); setReasonError('') }}
                            >
                              Reject
                            </Button>
                          </>}
                          {r.status === 'Pending' && r.raisedById === myId && (
                            <Button
                              size="sm"
                              variant="ghost"
                              icon={<XCircle className="h-4 w-4" />}
                              onClick={() => act.mutate({ id: r.id, action: 'cancel', note: 'Cancelled' })}
                            >
                              Cancel
                            </Button>
                          )}
                          {r.status === 'Approved' && canManage && (
                            <Button
                              size="sm"
                              icon={<CheckCircle2 className="h-4 w-4" />}
                              onClick={() => act.mutate({ id: r.id, action: 'complete' })}
                            >
                              Complete
                            </Button>
                          )}
                        </div>
                      </td>
                    </tr>
                  )) : (
                    <tr>
                      <td colSpan={8}>
                        <EmptyState
                          icon={<Wrench className="h-6 w-6" />}
                          title="No procurement requests"
                          hint={canManage ? 'Request a new asset or repair from the button above.' : 'No procurement requests yet.'}
                        />
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </Card>
          )}

          <div className="pt-2">
            <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-300 px-1 mb-2">Asset Assignments</h3>
            <Card className="overflow-hidden">
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="text-left text-xs uppercase tracking-wide text-slate-400 border-b border-[var(--line)]">
                      <th className="px-5 py-3">Asset</th>
                      <th className="px-5 py-3">Subject</th>
                      <th className="px-5 py-3">Assigned To</th>
                      <th className="px-5 py-3">Status</th>
                      <th className="px-5 py-3">Raised By</th>
                      <th className="px-5 py-3 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {assignmentRequests.length ? assignmentRequests.map((r) => (
                      <tr key={r.id} className="border-b border-[var(--line)] last:border-0 hover:bg-slate-50 dark:hover:bg-navy-600 transition-colors">
                        <td className="px-5 py-3 text-xs text-slate-600 dark:text-slate-300">
                          {r.assetName || '—'}
                        </td>
                        <td className="px-5 py-3">
                          <p className="font-semibold text-slate-900 dark:text-slate-100">{r.subject}</p>
                          <p className="text-xs text-slate-500 dark:text-slate-400 line-clamp-1">{r.description}</p>
                        </td>
                        <td className="px-5 py-3 text-xs text-slate-600 dark:text-slate-300">
                          {r.targetUserName || '—'}
                        </td>
                        <td className="px-5 py-3">
                          <StatusPill value={r.status} />
                        </td>
                        <td className="px-5 py-3 text-xs text-slate-600 dark:text-slate-300">
                          {r.raisedByName}
                        </td>
                        <td className="px-5 py-3 text-right">
                          <div className="flex items-center gap-2 justify-end">
                            {(r.status === 'PendingAcceptance' || r.status === 'Pending') && r.targetUserId === myId && <>
                              <Button
                                size="sm"
                                variant="secondary"
                                icon={<CheckCircle2 className="h-4 w-4" />}
                                onClick={() => act.mutate({ id: r.id, action: 'accept' })}
                              >
                                Accept
                              </Button>
                              <Button
                                size="sm"
                                variant="ghost"
                                icon={<XCircle className="h-4 w-4" />}
                                onClick={() => act.mutate({ id: r.id, action: 'decline' })}
                              >
                                Decline
                              </Button>
                            </>}
                            {r.status === 'Pending' && !r.targetUserId && canManage && (
                              <Button
                                size="sm"
                                variant="ghost"
                                icon={<XCircle className="h-4 w-4" />}
                                onClick={() => act.mutate({ id: r.id, action: 'cancel' })}
                              >
                                Cancel
                              </Button>
                            )}
                            {(r.status === 'PendingAcceptance' || (r.status === 'Pending' && r.targetUserId)) && canManage && r.targetUserId !== myId && (
                              <span className="text-xs text-slate-400">Awaiting employee acceptance</span>
                            )}
                          </div>
                        </td>
                      </tr>
                    )) : (
                      <tr>
                        <td colSpan={6}>
                          <EmptyState
                            icon={<UserPlus className="h-6 w-6" />}
                            title="No assignments"
                            hint={canManage ? 'Assign an available asset to an employee to get started.' : 'No asset assignments yet.'}
                          />
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </Card>
          </div>
        </div>
      )}

      {/* Add Asset Modal */}
      {assetOpen && (
        <Modal open={assetOpen} onClose={() => setAssetOpen(false)} title="Add New Asset"
          footer={
            <>
              <Button variant="secondary" onClick={() => setAssetOpen(false)}>Cancel</Button>
              <Button loading={createAsset.isPending} disabled={!assetForm.name} onClick={() => createAsset.mutate()}>
                Add Asset
              </Button>
            </>
          }>
          <div className="space-y-4">
            <div className="grid sm:grid-cols-2 gap-4">
              <div className="sm:col-span-2">
                <Field label="Asset Name" required>
                  <Input value={assetForm.name} onChange={(e) => setAssetForm({ ...assetForm, name: e.target.value })} placeholder="e.g. Dell Latitude 5420" />
                </Field>
              </div>
              <div className="sm:col-span-2">
                <Field label="Description">
                  <Textarea value={assetForm.description} onChange={(e) => setAssetForm({ ...assetForm, description: e.target.value })} rows={2} />
                </Field>
              </div>
              <Field label="Category">
                <Select value={assetForm.category} onChange={(e) => setAssetForm({ ...assetForm, category: e.target.value })}>
                  {enums.data?.assetCategories.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                </Select>
              </Field>
              <Field label="Condition">
                <Select value={assetForm.condition} onChange={(e) => setAssetForm({ ...assetForm, condition: e.target.value })}>
                  {enums.data?.assetConditions.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                </Select>
              </Field>
              <Field label="Serial Number">
                <Input value={assetForm.serialNumber} onChange={(e) => setAssetForm({ ...assetForm, serialNumber: e.target.value })} />
              </Field>
              <Field label="Purchase Date">
                <Input type="date" value={assetForm.purchaseDate} onChange={(e) => setAssetForm({ ...assetForm, purchaseDate: e.target.value })} />
              </Field>
              <Field label="Purchase Price (₹)">
                <Input type="number" min={0} value={assetForm.purchasePrice} onChange={(e) => setAssetForm({ ...assetForm, purchasePrice: e.target.value })} />
              </Field>
            </div>
          </div>
        </Modal>
      )}

      {/* New Request Modal */}
      {requestOpen && (
        <Modal open={requestOpen} onClose={() => setRequestOpen(false)} title="Request New Asset / Repair"
          footer={
            <>
              <Button variant="secondary" onClick={() => setRequestOpen(false)}>Cancel</Button>
              <Button loading={createRequest.isPending} disabled={!requestForm.subject || !requestForm.description} onClick={() => createRequest.mutate()}>
                Submit Request
              </Button>
            </>
          }>
          <div className="space-y-4">
            <div className="grid sm:grid-cols-2 gap-4">
              <div className="sm:col-span-2">
                <Field label="Subject" required>
                  <Input value={requestForm.subject} onChange={(e) => setRequestForm({ ...requestForm, subject: e.target.value })} placeholder="e.g. New Dell Laptop for design team" />
                </Field>
              </div>
              <Field label="Request Type">
                <Select value={requestForm.requestType} onChange={(e) => setRequestForm({ ...requestForm, requestType: e.target.value })}>
                  {REQUEST_TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
                </Select>
              </Field>
              <Field label="Asset (optional)">
                <Select value={requestForm.assetId} onChange={(e) => setRequestForm({ ...requestForm, assetId: e.target.value })}>
                  <option value="">— select asset —</option>
                  {assetList.map((a) => <option key={a.id} value={a.id}>{a.name} ({a.serialNumber || 'no serial'})</option>)}
                </Select>
              </Field>
              <Field label="Quantity">
                <Input type="number" min={1} value={requestForm.quantity} onChange={(e) => setRequestForm({ ...requestForm, quantity: e.target.value })} />
              </Field>
              <Field label="Estimated Cost (₹)">
                <Input type="number" min={0} value={requestForm.estimatedCost} onChange={(e) => setRequestForm({ ...requestForm, estimatedCost: e.target.value })} />
              </Field>
              <Field label="Vendor">
                <Input value={requestForm.vendor} onChange={(e) => setRequestForm({ ...requestForm, vendor: e.target.value })} />
              </Field>
              <div className="sm:col-span-2">
                <Field label="Description" required>
                  <Textarea value={requestForm.description} onChange={(e) => setRequestForm({ ...requestForm, description: e.target.value })} rows={3} />
                </Field>
              </div>
            </div>
          </div>
        </Modal>
      )}

      {/* Reject Request Modal */}
      {rejecting && (
        <Modal open={!!rejecting} onClose={() => setRejecting(null)} title="Reject Request" subtitle={rejecting.subject} size="sm"
          footer={
            <>
              <Button variant="secondary" onClick={() => setRejecting(null)}>Cancel</Button>
              <Button variant="danger" icon={<XCircle className="h-4 w-4" />} loading={act.isPending}
                onClick={() => {
                  const r = reason.trim()
                  if (!r) { setReasonError('Please provide a reason before rejecting.'); return }
                  act.mutate({ id: rejecting!.id, action: 'reject', note: r })
                  setRejecting(null)
                }}>
                Confirm rejection
              </Button>
            </>
          }>
          <div className="space-y-4">
            <Field label="Reason for rejection" required error={reasonError}>
              <Textarea rows={4} value={reason} onChange={(e) => { setReason(e.target.value); if (reasonError) setReasonError('') }}
                placeholder="Why are you rejecting this request?" autoFocus />
            </Field>
          </div>
        </Modal>
      )}

      {/* Assign Asset Modal */}
      {assigning && (
        <Modal open={!!assigning} onClose={() => setAssigning(null)} title={`Request Assignment`} subtitle={assigning.name} size="sm"
          footer={
            <>
              <Button variant="secondary" onClick={() => setAssigning(null)}>Cancel</Button>
              <Button loading={requestAssignment.isPending} disabled={!assignUserId} onClick={() => requestAssignment.mutate({ assetId: assigning.id, targetUserId: Number(assignUserId), subject: `Assign ${assigning.name}` })}>
                Send Request
              </Button>
            </>
          }>
          <div className="space-y-4">
            <Field label="Assign to" required>
              <Select value={assignUserId} onChange={(e) => setAssignUserId(e.target.value)}>
                <option value="">— select employee —</option>
                {users?.map((u) => <option key={u.id} value={u.id}>{u.name} ({u.employeeCode})</option>)}
              </Select>
            </Field>
            <p className="text-xs text-slate-500">
              The employee will receive a notification and can accept or reject the assignment.
            </p>
          </div>
        </Modal>
      )}
    </div>
  )
}
