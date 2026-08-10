import { describe, expect, it } from 'vitest'
import { band, bandTone, localScore, type FormArea } from './appraisalForms'

const areas: FormArea[] = [
  { name: 'Delivery', type: 'Rating', weight: 70, allowNa: false },
  { name: 'Client', type: 'Rating', weight: 30, allowNa: true },
  { name: 'Comment', type: 'Text', weight: 0, allowNa: false },
]

describe('appraisal form calculations', () => {
  it('calculates a plain average on the 0-100 scale', () => {
    expect(localScore(areas, false, { Delivery: { areaName: 'Delivery', rating: 5 }, Client: { areaName: 'Client', rating: 3 } })).toBe(80)
  })

  it('calculates weighted ratings', () => {
    expect(localScore(areas, true, { Delivery: { areaName: 'Delivery', rating: 5 }, Client: { areaName: 'Client', rating: 3 } })).toBe(88)
  })

  it('redistributes weight when an area is not applicable', () => {
    expect(localScore(areas, true, { Delivery: { areaName: 'Delivery', rating: 4 }, Client: { areaName: 'Client', rating: 1, notApplicable: true } })).toBe(80)
  })

  it('returns null without usable ratings', () => {
    expect(localScore(areas, true, {})).toBeNull()
  })

  it('maps score boundaries to labels and tones', () => {
    expect(band(90)).toBe('Outstanding')
    expect(band(70)).toBe('Meets Expectations')
    expect(band(59)).toBe('Unsatisfactory')
    expect(bandTone(80)).toBe('good')
    expect(bandTone(60)).toBe('warn')
    expect(bandTone(59)).toBe('danger')
  })
})
