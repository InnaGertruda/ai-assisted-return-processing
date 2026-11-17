import assert from 'node:assert/strict'
import test from 'node:test'
import { businessRuleById, businessRules } from './businessRules.mjs'
import { outcomeLabel, stageTone } from './tracePresentation.mjs'

test('dry-run outcome always includes the actual commit status', () => {
  assert.equal(
    outcomeLabel({ finalOutcome: 'EligibleForCommit', commitStatus: 'Not committed' }),
    'EligibleForCommit · Not committed',
  )
  assert.equal(stageTone('Skipped'), 'text-white/45')
})

test('accepted outcome is labeled with committed state', () => {
  assert.equal(
    outcomeLabel({ finalOutcome: 'Accepted', commitStatus: 'Committed' }),
    'Accepted · Committed',
  )
})

test('rule catalog covers every documented MVP rule and resolves trace references', () => {
  assert.deepEqual(
    businessRules.map((rule) => rule.id),
    [
      'BR-RET-001',
      'BR-RET-002',
      'BR-RET-003',
      'BR-RET-004',
      'BR-RET-005',
      'BR-RET-006',
      'BR-RET-007',
      'BR-RET-008',
      'BR-RET-010',
      'BR-AUD-001',
    ],
  )

  assert.equal(businessRuleById('BR-RET-008')?.title, 'Ambiguity blocks automatic creation')
  assert.equal(businessRuleById('BR-UNKNOWN'), undefined)
})
