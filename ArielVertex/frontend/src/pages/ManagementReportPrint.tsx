import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, Printer, Loader2 } from 'lucide-react'
import { api } from '../lib/api'
import { asArray, fmtDate } from '../ui/util'

const healthColor = (h: string) => (h === 'Green' ? '#10b981' : h === 'Amber' ? '#f59e0b' : '#f43f5e')

export default function ManagementReportPrint() {
  const navigate = useNavigate()
  const projects = useQuery({
    queryKey: ['projects', 'reports', 'management-print'],
    queryFn: async () => asArray((await api.get('/projects', { params: { pageSize: 100 } })).data?.items) as any[],
  })
  const occ = useQuery({ queryKey: ['occupancy'], queryFn: async () => { try { return (await api.get('/resources/occupancy')).data as any } catch { return null } } })

  useEffect(() => { document.title = 'Management Report — Ariel Vertex' }, [])
  if (projects.isLoading) return <div style={{ minHeight: '100vh', display: 'grid', placeItems: 'center' }}><Loader2 className="h-6 w-6 animate-spin" /></div>

  const ps = asArray(projects.data)
  const occupancyBuckets = asArray(occ.data?.buckets)
  const active = ps.filter((p) => p.status === 'Active').length
  const atRisk = ps.filter((p) => p.health !== 'Green').length
  const today = new Date()

  return (
    <div className="report-root">
      <style>{`
        .report-root{background:#eef2f8;min-height:100vh;padding:24px;color:#0f172a;font-family:ui-sans-serif,system-ui,'Segoe UI',sans-serif}
        .toolbar{max-width:900px;margin:0 auto 16px;display:flex;justify-content:space-between;gap:12px}
        .btn{display:inline-flex;align-items:center;gap:8px;border-radius:10px;padding:9px 15px;font-weight:600;font-size:14px;cursor:pointer;border:1px solid #d7dee9;background:#fff;color:#0f172a}
        .btn.primary{background:#1E7FD4;color:#fff;border-color:#1E7FD4}
        .doc{max-width:900px;margin:0 auto;background:#fff;border:1px solid #e2e8f2;border-radius:14px;overflow:hidden;box-shadow:0 20px 50px -30px rgba(15,23,42,.3)}
        .band{height:6px;background:linear-gradient(90deg,#17203E,#1E7FD4)}
        .pad{padding:40px 44px}
        .brow{display:flex;align-items:center;justify-content:space-between;border-bottom:1px solid #eef2f7;padding-bottom:20px;margin-bottom:24px}
        .brand{display:flex;align-items:center;gap:10px}.brand b{font-size:17px;font-weight:800}.brand b span{color:#1E7FD4}
        h1{font-size:24px;font-weight:800;margin:0 0 4px}.sub{color:#64748b;font-size:14px;margin:0 0 24px}
        .kpis{display:grid;grid-template-columns:repeat(4,1fr);gap:14px;margin-bottom:26px}
        .kpi{border:1px solid #eef2f7;border-radius:12px;padding:14px 16px}
        .kpi .n{font-size:28px;font-weight:800;font-variant-numeric:tabular-nums}.kpi .l{font-size:11px;text-transform:uppercase;letter-spacing:.06em;color:#94a3b8;font-weight:700}
        h2{font-size:12px;font-weight:700;letter-spacing:.1em;text-transform:uppercase;color:#475569;margin:26px 0 12px}
        table{width:100%;border-collapse:collapse}td,th{padding:9px 8px;font-size:13px;text-align:left;border-bottom:1px solid #f1f5f9}
        th{font-size:11px;text-transform:uppercase;letter-spacing:.05em;color:#94a3b8;font-weight:700}
        .dot{display:inline-block;width:9px;height:9px;border-radius:50%;margin-right:7px;vertical-align:middle}
        .num{text-align:right;font-variant-numeric:tabular-nums}
        .foot{margin-top:30px;padding-top:16px;border-top:1px solid #eef2f7;font-size:12px;color:#94a3b8;display:flex;justify-content:space-between}
        @media print{.report-root{background:#fff;padding:0}.toolbar{display:none}.doc{border:none;box-shadow:none;border-radius:0;max-width:none}@page{margin:16mm}}
      `}</style>

      <div className="toolbar">
        <button className="btn" onClick={() => navigate(-1)}><ArrowLeft className="h-4 w-4" /> Back</button>
        <button className="btn primary" onClick={() => window.print()}><Printer className="h-4 w-4" /> Save as PDF</button>
      </div>

      <div className="doc">
        <div className="band" />
        <div className="pad">
          <div className="brow">
            <div className="brand">
              <svg width="30" height="30" viewBox="0 0 64 64" aria-hidden="true"><path d="M6 12 L26 12 L20 26 L12 26 Z" fill="#1E7FD4" /><path d="M20 12 L34 12 L24 52 Z" fill="#17203E" /></svg>
              <b>Ariel <span>Vertex</span></b>
            </div>
            <span style={{ fontSize: 12, color: '#94a3b8' }}>{fmtDate(today.toISOString())}</span>
          </div>

          <h1>Management Report</h1>
          <p className="sub">Portfolio health and resource summary across all visible projects</p>

          <div className="kpis">
            <div className="kpi"><div className="n">{ps.length}</div><div className="l">Projects</div></div>
            <div className="kpi"><div className="n">{active}</div><div className="l">Active</div></div>
            <div className="kpi"><div className="n" style={{ color: atRisk ? '#f59e0b' : '#10b981' }}>{atRisk}</div><div className="l">At risk</div></div>
            <div className="kpi"><div className="n">{ps.reduce((a, p) => a + (p.memberCount || 0), 0)}</div><div className="l">Assignments</div></div>
          </div>

          <h2>Project register</h2>
          <table>
            <thead><tr><th>Code</th><th>Project</th><th>Client</th><th>Status</th><th>Health</th><th className="num">Team</th></tr></thead>
            <tbody>
              {ps.map((p) => (
                <tr key={p.id}>
                  <td style={{ fontWeight: 700 }}>{p.code}</td><td>{p.name}</td><td>{p.clientName || '—'}</td><td>{p.status}</td>
                  <td><span className="dot" style={{ background: healthColor(p.health) }} />{p.health}</td>
                  <td className="num">{p.memberCount}</td>
                </tr>
              ))}
            </tbody>
          </table>

          {occupancyBuckets.length > 0 && (
            <>
              <h2>Resource availability</h2>
              <table>
                <thead><tr><th>Bucket</th><th className="num">People</th></tr></thead>
                <tbody>{occupancyBuckets.map((b: any) => (<tr key={b.bucket}><td>{b.bucket}</td><td className="num">{b.count}</td></tr>))}</tbody>
              </table>
            </>
          )}

          <div className="foot">
            <span>Generated by Ariel Vertex · Operations, Review &amp; Performance</span>
            <span>Confidential — internal use</span>
          </div>
        </div>
      </div>
    </div>
  )
}
