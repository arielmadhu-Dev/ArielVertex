import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { UsersRound, Search, Shield } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import { useEnums } from '../lib/hooks'
import type { Paged, UserListItem } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, Badge, Avatar, StatusPill, EmptyState, Skeleton } from '../ui/primitives'
import { Input, Select } from '../ui/form'
import { useToast } from '../ui/Toast'
import { fmtDate } from '../ui/util'
import { P } from '../components/nav'

export default function Employees() {
  const { has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const enums = useEnums()
  const [search, setSearch] = useState('')
  const canManage = has(P.EmployeesManage)

  const { data, isLoading } = useQuery({
    queryKey: ['employees', search],
    queryFn: async () => (await api.get<Paged<UserListItem>>('/employees', { params: { search, pageSize: 100 } })).data,
  })

  const changeRole = useMutation({
    mutationFn: ({ id, role }: { id: number; role: string }) => api.put(`/employees/${id}/role`, { role }),
    onSuccess: () => { push('Role updated'); qc.invalidateQueries({ queryKey: ['employees'] }) },
    onError: (e) => push(apiError(e), 'error'),
  })

  return (
    <div>
      <PageHeader title="Employees" subtitle="Directory synced from Microsoft Entra (local mode today)" icon={<UsersRound className="h-5 w-5" />} />

      <div className="relative mb-4 max-w-sm">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
        <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search people…" className="pl-9" />
      </div>

      <Card className="overflow-hidden">
        <div className="overflow-x-auto av-scroll-x">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-xs uppercase tracking-wide text-slate-400 border-b border-[var(--line)]">
                <th className="px-5 py-3 font-semibold">Employee</th>
                <th className="px-5 py-3 font-semibold hidden md:table-cell">Department</th>
                <th className="px-5 py-3 font-semibold hidden lg:table-cell">Manager</th>
                <th className="px-5 py-3 font-semibold">Role</th>
                <th className="px-5 py-3 font-semibold hidden sm:table-cell">Status</th>
              </tr>
            </thead>
            <tbody>
              {isLoading ? [...Array(6)].map((_, i) => <tr key={i}><td colSpan={5} className="px-5 py-2"><Skeleton className="h-10" /></td></tr>)
                : data?.items.map((u) => (
                  <tr key={u.id} className="border-b border-[var(--line)] last:border-0 hover:bg-slate-50 dark:hover:bg-navy-600">
                    <td className="px-5 py-3">
                      <div className="flex items-center gap-3">
                        <Avatar name={u.name} color={u.avatarColor} size={38} />
                        <div><p className="font-semibold">{u.name}</p><p className="text-xs text-slate-500">{u.designation} · {u.employeeCode}</p></div>
                      </div>
                    </td>
                    <td className="px-5 py-3 hidden md:table-cell text-slate-500">{u.department || '—'}</td>
                    <td className="px-5 py-3 hidden lg:table-cell text-slate-500">{u.managerName || '—'}</td>
                    <td className="px-5 py-3">
                      {canManage
                        ? <Select value={u.role} onChange={(e) => changeRole.mutate({ id: u.id, role: e.target.value })} className="w-44 !py-1.5 text-xs">{enums.data?.roles.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select>
                        : <Badge t="brand">{u.roleLabel}</Badge>}
                    </td>
                    <td className="px-5 py-3 hidden sm:table-cell"><StatusPill value={u.status} /></td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>
        {!isLoading && !data?.items.length && <EmptyState icon={<UsersRound className="h-6 w-6" />} title="No employees found" />}
      </Card>
      {canManage && <p className="mt-3 text-xs text-slate-400 inline-flex items-center gap-1.5"><Shield className="h-3.5 w-3.5" />Role changes are audited and take effect on the employee's next sign-in.</p>}
    </div>
  )
}
