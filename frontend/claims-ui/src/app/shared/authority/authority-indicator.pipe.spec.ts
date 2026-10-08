import { authorityIndicator } from './authority-indicator.pipe';

describe('authorityIndicator', () => {
  it('uses the inclusive auto-approval threshold', () => expect(authorityIndicator(10_000).level).toBe('auto'));
  it('uses the inclusive supervisor threshold', () => expect(authorityIndicator(100_000).level).toBe('supervisor'));
  it('requires a manager above the supervisor threshold', () => expect(authorityIndicator(100_000.01).level).toBe('manager'));
  it('evaluates signed deltas by absolute value', () => expect(authorityIndicator(-10_001).level).toBe('supervisor'));
});
