import { useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, Printer, Loader2 } from 'lucide-react'
import { api } from '../lib/api'
import type { PerformanceReport } from '../lib/types'
import { fmtDate } from '../ui/util'

const bandColor = (s: number) => (s >= 90 ? '#059669' : s >= 80 ? '#10b981' : s >= 70 ? '#1E7FD4' : s >= 60 ? '#f59e0b' : '#f43f5e')

export default function PerformanceReportPrint() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { data: r, isLoading, isError } = useQuery({
    queryKey: ['report-print', id],
    queryFn: async () => (await api.get<PerformanceReport>(`/performance/report/${id}`)).data,
  })

  useEffect(() => { document.title = r ? `Performance Report — ${r.subjectName} — ${r.period}` : 'Performance Report' }, [r])

  if (isLoading) return <div style={{ minHeight: '100vh', display: 'grid', placeItems: 'center' }}><Loader2 className="h-6 w-6 animate-spin" /></div>
  if (isError || !r) return <div style={{ minHeight: '100vh', display: 'grid', placeItems: 'center', color: '#64748b' }}>Report unavailable or access denied.</div>

  const color = bandColor(r.overallScore)

  return (
    <div className="report-root">
      <style>{`
        .report-root{background:#eef2f8;min-height:100vh;padding:24px;color:#0f172a;font-family:ui-sans-serif,system-ui,'Segoe UI',sans-serif}
        .toolbar{max-width:820px;margin:0 auto 16px;display:flex;justify-content:space-between;gap:12px}
        .btn{display:inline-flex;align-items:center;gap:8px;border-radius:10px;padding:9px 15px;font-weight:600;font-size:14px;cursor:pointer;border:1px solid #d7dee9;background:#fff;color:#0f172a}
        .btn.primary{background:#1E7FD4;color:#fff;border-color:#1E7FD4}
        .doc{max-width:820px;margin:0 auto;background:#fff;border:1px solid #e2e8f2;border-radius:14px;overflow:hidden;box-shadow:0 20px 50px -30px rgba(15,23,42,.3)}
        .band{height:6px;background:linear-gradient(90deg,#17203E,#1E7FD4)}
        .pad{padding:40px 44px}
        .brow{display:flex;align-items:center;justify-content:space-between;gap:16px;border-bottom:1px solid #eef2f7;padding-bottom:20px;margin-bottom:24px}
        .brand{display:flex;align-items:center;gap:10px}
        .brand b{font-size:17px;font-weight:800;letter-spacing:-.01em}
        .brand b span{color:#1E7FD4}
        .conf{font-size:10px;font-weight:700;letter-spacing:.14em;text-transform:uppercase;color:#94a3b8;border:1px solid #e2e8f2;border-radius:999px;padding:5px 11px}
        h1{font-size:24px;font-weight:800;letter-spacing:-.02em;margin:0 0 4px}
        .sub{color:#64748b;font-size:14px;margin:0 0 24px}
        .scorewrap{display:flex;align-items:center;gap:24px;padding:20px 24px;border:1px solid #eef2f7;border-radius:12px;background:#f8fafc;margin-bottom:26px}
        .score{font-size:48px;font-weight:800;line-height:1;font-variant-numeric:tabular-nums}
        .of{font-size:13px;color:#94a3b8}
        .chip{display:inline-block;font-weight:700;font-size:13px;padding:6px 12px;border-radius:999px;color:#fff}
        h2{font-size:12px;font-weight:700;letter-spacing:.1em;text-transform:uppercase;color:#475569;margin:26px 0 12px}
        table{width:100%;border-collapse:collapse}
        td,th{padding:9px 0;font-size:14px;text-align:left;border-bottom:1px solid #f1f5f9}
        th{font-size:11px;text-transform:uppercase;letter-spacing:.06em;color:#94a3b8;font-weight:700}
        .bar{height:8px;border-radius:999px;background:#eef2f7;overflow:hidden;width:180px}
        .bar>span{display:block;height:100%;border-radius:999px}
        .num{text-align:right;font-weight:700;font-variant-numeric:tabular-nums}
        .na{color:#94a3b8;font-weight:600;font-size:12px}
        .prose{font-size:14px;line-height:1.6;color:#334155;margin:0}
        .grid2{display:grid;grid-template-columns:1fr 1fr;gap:22px}
        .foot{margin-top:30px;padding-top:16px;border-top:1px solid #eef2f7;font-size:12px;color:#94a3b8;display:flex;justify-content:space-between;gap:12px}
        @media print{
          .report-root{background:#fff;padding:0}
          .toolbar{display:none}
          .doc{border:none;box-shadow:none;border-radius:0;max-width:none}
          @page{margin:16mm}
        }
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
            <span className="conf">Confidential</span>
          </div>

          <h1>Performance Report</h1>
          <p className="sub">{r.subjectName} · {r.periodType} · {r.period}{r.publishedAt ? ` · Published ${fmtDate(r.publishedAt)}` : ' · Draft (not yet published)'}</p>

          <div className="scorewrap">
            <div>
              <div className="score" style={{ color }}>{r.overallScore}<span className="of"> / 100</span></div>
            </div>
            <div>
              <span className="chip" style={{ background: color }}>{r.ratingLabel}</span>
              <p className="prose" style={{ marginTop: 10, fontSize: 13 }}>Scores are indicators derived from operational evidence, reviewed and approved by HR. They support development conversations and are not an automatic final judgement.</p>
            </div>
          </div>

          <h2>Category breakdown</h2>
          <table>
            <thead><tr><th>Competency</th><th style={{ width: 200 }}>Score</th><th className="num">Value</th><th className="num" style={{ width: 70 }}>Weight</th></tr></thead>
            <tbody>
              {r.categories.map((c) => (
                <tr key={c.category}>
                  <td>{c.categoryName}</td>
                  <td>{c.notApplicable ? <span className="na">Not applicable</span> : <div className="bar"><span style={{ width: `${c.score}%`, background: bandColor(c.score) }} /></div>}</td>
                  <td className="num">{c.notApplicable ? '—' : c.score}</td>
                  <td className="num">{c.weight}%</td>
                </tr>
              ))}
            </tbody>
          </table>

          <div className="grid2">
            <div><h2>Strengths</h2><p className="prose">{r.strengths || '—'}</p></div>
            <div><h2>Improvement areas</h2><p className="prose">{r.improvementAreas || '—'}</p></div>
          </div>
          <h2>Recommended actions</h2>
          <p className="prose">{r.recommendedActions || '—'}</p>

          <h2>Data sources</h2>
          <p className="prose">{r.dataSources}</p>

          <div className="foot">
            <span>Generated by Ariel Vertex · Operations, Review &amp; Performance</span>
            <span>{r.subjectName} · {r.period}</span>
          </div>
        </div>
      </div>
    </div>
  )
}
