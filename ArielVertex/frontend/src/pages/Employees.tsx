import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { UsersRound, Search, Shield, Pencil, Cloud, HardDrive } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import { useEnums } from '../lib/hooks'
import type { Paged, UserListItem } from '../lib/types'
import { PageHeader } from '../components/PageHeader'
import { Card, Badge, Avatar, StatusPill, EmptyState, Skeleton, Button } from '../ui/primitives'
import { Field, Input, Select, Textarea } from '../ui/form'
import { Modal } from '../ui/Modal'
import { useToast } from '../ui/Toast'
import { P } from '../components/nav'

interface EditOption { id: number; name: string; detail?: string }
interface EditOptions { departments: EditOption[]; managers: EditOption[] }
interface EmployeeForm {
  name: string
  employeeCode: string
  designation: string
  skills: string
  status: string
  joiningDate: string
  departmentId: string
  managerId: string
  profileManagedLocally: boolean
  managerManagedLocally: boolean
}

const emptyForm: EmployeeForm = {
  name: '', employeeCode: '', designation: '', skills: '', status: 'Active',
  joiningDate: '', departmentId: '', managerId: '',
  profileManagedLocally: false, managerManagedLocally: false,
}

export default function Employees() {
  const { has } = useAuth()
  const { push } = useToast()
  const qc = useQueryClient()
  const enums = useEnums()
  const [search, setSearch] = useState('')
  const [editing, setEditing] = useState<UserListItem | null>(null)
  const [form, setForm] = useState<EmployeeForm>(emptyForm)
  const canEdit = has(P.EmployeesManage)
  const canChangeRole = has(P.RolesManage)

  const { data, isLoading } = useQuery({
    queryKey: ['employees', search],
    queryFn: async () => (await api.get<Paged<UserListItem>>('/employees', { params: { search, pageSize: 100 } })).data,
  })
  const employees = Array.isArray(data?.items) ? data.items : []

  const editOptions = useQuery({
    queryKey: ['employee-edit-options'],
    enabled: canEdit,
    queryFn: async () => (await api.get<EditOptions>('/employees/edit-options')).data,
  })

  const changeRole = useMutation({
    mutationFn: ({ id, role }: { id: number; role: string }) => api.put(`/employees/${id}/role`, { role }),
    onSuccess: () => { push('Role updated'); qc.invalidateQueries({ queryKey: ['employees'] }) },
    onError: (e) => push(apiError(e), 'error'),
  })

  const saveEmployee = useMutation({
    mutationFn: ({ id, payload }: { id: number; payload: Record<string, unknown> }) =>
      api.put<UserListItem>(`/employees/${id}`, payload),
    onSuccess: () => {
      push('Employee profile updated')
      setEditing(null)
      qc.invalidateQueries({ queryKey: ['employees'] })
      qc.invalidateQueries({ queryKey: ['employee-edit-options'] })
    },
    onError: (e) => push(apiError(e), 'error'),
  })

  const openEditor = (employee: UserListItem) => {
    setEditing(employee)
    setForm({
      name: employee.name,
      employeeCode: employee.employeeCode,
      designation: employee.designation,
      skills: employee.skills || '',
      status: employee.status,
      joiningDate: employee.joiningDate?.slice(0, 10) || '',
      departmentId: employee.departmentId?.toString() || '',
      managerId: employee.managerId?.toString() || '',
      profileManagedLocally: employee.profileManagedLocally,
      managerManagedLocally: employee.managerManagedLocally,
    })
  }

  const setProfileField = (values: Partial<EmployeeForm>) =>
    setForm(current => ({
      ...current,
      ...values,
      profileManagedLocally: editing?.isProvisionedFromEntra ? true : current.profileManagedLocally,
    }))

  const submit = () => {
    if (!editing || !form.name.trim() || !form.employeeCode.trim() || !form.designation.trim() || !form.joiningDate) {
      push('Name, employee code, designation, and joining date are required.', 'error')
      return
    }

    saveEmployee.mutate({
      id: editing.id,
      payload: {
        name: form.name.trim(),
        employeeCode: form.employeeCode.trim(),
        designation: form.designation.trim(),
        skills: form.skills.trim(),
        status: form.status,
        joiningDate: `${form.joiningDate}T00:00:00Z`,
        departmentId: form.departmentId ? Number(form.departmentId) : null,
        managerId: form.managerId ? Number(form.managerId) : null,
        profileManagedLocally: form.profileManagedLocally,
        managerManagedLocally: form.managerManagedLocally,
      },
    })
  }

  return (
    <div>
      <PageHeader title="Employees" subtitle="Microsoft Entra directory with HR-managed profile overrides" icon={<UsersRound className="h-5 w-5" />} />

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
                {canEdit && <th className="px-5 py-3 font-semibold text-right">Actions</th>}
              </tr>
            </thead>
            <tbody>
              {isLoading ? [...Array(6)].map((_, i) => (
                <tr key={i}><td colSpan={canEdit ? 6 : 5} className="px-5 py-2"><Skeleton className="h-10" /></td></tr>
              )) : employees.map((employee) => (
                <tr key={employee.id} className="border-b border-[var(--line)] last:border-0 hover:bg-slate-50 dark:hover:bg-navy-600">
                  <td className="px-5 py-3">
                    <div className="flex items-center gap-3">
                      <Avatar name={employee.name} color={employee.avatarColor} size={38} />
                      <div>
                        <p className="font-semibold">{employee.name}</p>
                        <p className="text-xs text-slate-500">{employee.designation} · {employee.employeeCode}</p>
                      </div>
                    </div>
                  </td>
                  <td className="px-5 py-3 hidden md:table-cell text-slate-500">{employee.department || '—'}</td>
                  <td className="px-5 py-3 hidden lg:table-cell text-slate-500">
                    <span>{employee.managerName || '—'}</span>
                    {employee.managerManagedLocally && <HardDrive className="inline ml-1.5 h-3.5 w-3.5 text-brand-500" aria-label="Locally managed" />}
                  </td>
                  <td className="px-5 py-3">
                    {canChangeRole
                      ? <Select value={employee.role} onChange={(e) => changeRole.mutate({ id: employee.id, role: e.target.value })} className="w-44 !py-1.5 text-xs">
                          {enums.data?.roles.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                        </Select>
                      : <Badge t="brand">{employee.roleLabel}</Badge>}
                  </td>
                  <td className="px-5 py-3 hidden sm:table-cell"><StatusPill value={employee.status} /></td>
                  {canEdit && (
                    <td className="px-5 py-3 text-right">
                      <Button variant="ghost" size="sm" icon={<Pencil className="h-4 w-4" />} onClick={() => openEditor(employee)}>
                        Edit
                      </Button>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!isLoading && !employees.length && <EmptyState icon={<UsersRound className="h-6 w-6" />} title="No employees found" />}
      </Card>

      {canChangeRole && (
        <p className="mt-3 text-xs text-slate-400 inline-flex items-center gap-1.5">
          <Shield className="h-3.5 w-3.5" />Role changes are audited and take effect on the employee's next sign-in.
        </p>
      )}

      <Modal
        open={!!editing}
        onClose={() => !saveEmployee.isPending && setEditing(null)}
        title={editing ? `Edit ${editing.name}` : 'Edit employee'}
        subtitle={editing?.email}
        size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={() => setEditing(null)} disabled={saveEmployee.isPending}>Cancel</Button>
            <Button loading={saveEmployee.isPending} onClick={submit}>Save changes</Button>
          </>
        }>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Name" required>
            <Input value={form.name} onChange={(e) => setProfileField({ name: e.target.value })} />
          </Field>
          <Field label="Employee code" required hint="Local HR field; Entra sync does not overwrite it.">
            <Input value={form.employeeCode} onChange={(e) => setForm(current => ({ ...current, employeeCode: e.target.value }))} />
          </Field>
          <Field label="Designation" required>
            <Input value={form.designation} onChange={(e) => setProfileField({ designation: e.target.value })} />
          </Field>
          <Field label="Joining date" required>
            <Input type="date" value={form.joiningDate} onChange={(e) => setForm(current => ({ ...current, joiningDate: e.target.value }))} />
          </Field>
          <Field label="Department">
            <Select value={form.departmentId} onChange={(e) => setProfileField({ departmentId: e.target.value })}>
              <option value="">No department</option>
              {editOptions.data?.departments.map((option) => (
                <option key={option.id} value={option.id}>{option.name}{option.detail ? ` (${option.detail})` : ''}</option>
              ))}
            </Select>
          </Field>
          <Field label="Manager" hint="Changing this automatically enables a persistent local manager override.">
            <Select
              value={form.managerId}
              onChange={(e) => setForm(current => ({
                ...current,
                managerId: e.target.value,
                managerManagedLocally: editing?.isProvisionedFromEntra ? true : current.managerManagedLocally,
              }))}>
              <option value="">No manager</option>
              {editOptions.data?.managers.filter(option => option.id !== editing?.id).map((option) => (
                <option key={option.id} value={option.id}>{option.name}{option.detail ? ` — ${option.detail}` : ''}</option>
              ))}
            </Select>
          </Field>
          <Field label="Status">
            <Select value={form.status} onChange={(e) => setForm(current => ({ ...current, status: e.target.value }))}>
              {enums.data?.employeeStatuses.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
            </Select>
          </Field>
          <div className="hidden sm:block" />
          <div className="sm:col-span-2">
            <Field label="Skills" hint="Comma-separated skills; maintained locally by HR.">
              <Textarea value={form.skills} onChange={(e) => setForm(current => ({ ...current, skills: e.target.value }))} />
            </Field>
          </div>

          {editing?.isProvisionedFromEntra && (
            <div className="sm:col-span-2 rounded-xl border border-brand-100 bg-brand-50/60 p-4 dark:border-brand-900 dark:bg-brand-900/20">
              <div className="mb-3 flex items-center gap-2 text-sm font-semibold text-brand-700 dark:text-brand-200">
                <Cloud className="h-4 w-4" />Microsoft Entra sync ownership
              </div>
              <label className="flex items-start gap-3 text-sm">
                <input
                  type="checkbox"
                  className="mt-0.5 h-4 w-4"
                  checked={form.profileManagedLocally}
                  onChange={(e) => setForm(current => ({ ...current, profileManagedLocally: e.target.checked }))}
                />
                <span><strong>Keep profile fields locally managed.</strong><span className="block text-xs text-slate-500">Name, designation, and department will not be overwritten by Run sync.</span></span>
              </label>
              <label className="mt-3 flex items-start gap-3 text-sm">
                <input
                  type="checkbox"
                  className="mt-0.5 h-4 w-4"
                  checked={form.managerManagedLocally}
                  onChange={(e) => setForm(current => ({ ...current, managerManagedLocally: e.target.checked }))}
                />
                <span><strong>Keep manager locally managed.</strong><span className="block text-xs text-slate-500">The selected manager will not be replaced by Entra's manager relationship.</span></span>
              </label>
            </div>
          )}
        </div>
      </Modal>
    </div>
  )
}
