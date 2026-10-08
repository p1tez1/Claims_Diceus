import { ClaimStatus } from '../../core/models/api.models';
import { STATUS_PRESENTATIONS } from './status-badge.component';

describe('statusPresentation', () => {
  it('maps every backend status to its required colour class', () => {
    const expected: Record<ClaimStatus, string> = {
      Draft: 'status-draft', Open: 'status-open', UnderInvestigation: 'status-under-investigation',
      PendingPayment: 'status-pending-payment', Closed: 'status-closed', Reopened: 'status-reopened', Withdrawn: 'status-withdrawn'
    };
    expect(Object.keys(STATUS_PRESENTATIONS).length).toBe(7);
    for (const [status, cssClass] of Object.entries(expected)) expect(STATUS_PRESENTATIONS[status as ClaimStatus].cssClass).toBe(cssClass);
  });
});
