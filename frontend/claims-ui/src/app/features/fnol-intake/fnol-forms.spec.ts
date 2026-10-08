import { authorityIndicator } from '../../shared/authority/authority-indicator.pipe';
import { buildClaimRequest, createLossForm, createPartiesForm, createReviewForm, inForce, stepForField } from './fnol-forms';

describe('FNOL step forms', () => {
  it('rejects future loss dates and descriptions shorter than 20 trimmed characters', () => {
    const form = createLossForm();
    form.patchValue({ unknownPolicy: true, lossDate: '2099-01-01T10:00', causeOfLossCode: 'FIRE', lossDescription: '   short description  ' });
    expect(form.controls.lossDate.hasError('futureDate')).toBeTrue();
    expect(form.controls.lossDescription.hasError('minDescription')).toBeTrue();
    form.patchValue({ lossDate: '2026-01-01T10:00', lossDescription: 'A detailed loss description.' });
    expect(form.valid).toBeTrue();
  });

  it('rejects loss dates outside the supported year range', () => {
    const form = createLossForm();
    form.controls.lossDate.setValue('222222-02-22T10:00');
    expect(form.controls.lossDate.hasError('dateRange')).toBeTrue();
    form.controls.lossDate.setValue('1899-12-31T23:59');
    expect(form.controls.lossDate.hasError('dateRange')).toBeTrue();
    form.controls.lossDate.setValue('2101-01-01T00:00');
    expect(form.controls.lossDate.hasError('dateRange')).toBeTrue();
  });

  it('requires a selected policy or Unknown Policy and at least one claimant', () => {
    const loss = createLossForm(), people = createPartiesForm();
    expect(loss.hasError('policyRequired')).toBeTrue();
    loss.controls.unknownPolicy.setValue(true);
    expect(loss.hasError('policyRequired')).toBeFalse();
    expect(people.hasError('claimantRequired')).toBeFalse();
    expect(people.controls.parties.hasError('claimantRequired')).toBeTrue();
    people.controls.parties.setValue([{ role: 'Witness', type: 'Person', firstName: 'A', lastName: 'B' }]);
    expect(people.invalid).toBeTrue();
    people.controls.parties.setValue([{ role: 'Claimant', type: 'Person', firstName: 'A', lastName: 'B' }]);
    expect(people.valid).toBeTrue();
  });

  it('evaluates the policy period inclusively using the UTC loss date', () => {
    expect(inForce('2026-12-31T23:00:00Z', '2024-01-01', '2026-12-31')).toBeTrue();
    expect(inForce('2027-01-01T00:00:00Z', '2024-01-01', '2026-12-31')).toBeFalse();
    expect(inForce('2026-01-01T00:00:00Z', '2023-01-01', '2023-12-31')).toBeFalse();
  });

  it('builds the complete payload and omits reserve for Unknown Policy', () => {
    const loss = createLossForm(), people = createPartiesForm(), review = createReviewForm();
    loss.patchValue({ policyId: 'policy-id', lossDate: '2026-01-01T10:00', lossDescription: 'A detailed loss description.', causeOfLossCode: 'FIRE', lossLocation: 'Kyiv', estimatedLossAmount: 500 });
    people.controls.parties.setValue([{ role: 'Claimant', type: 'Person', firstName: 'Ada', lastName: 'Smith' }]);
    people.controls.riskObjects.setValue([{ assetType: 'Property', assetDescription: 'Building', damageDescription: 'Roof', assetReference: 'A1', isPrimary: true }]);
    review.patchValue({ enabled: true, component: 'Expense', amount: 12000, changeReason: 'Initial estimate' });
    const payload = buildClaimRequest(loss, people, review);
    expect(payload.policyId).toBe('policy-id'); expect(payload.parties.length).toBe(1); expect(payload.riskObjects.length).toBe(1);
    expect(payload.initialReserve).toEqual({ component: 'Expense', amount: 12000, changeReason: 'Initial estimate' });
    loss.controls.unknownPolicy.setValue(true);
    expect(buildClaimRequest(loss, people, review).initialReserve).toBeUndefined();
    expect(buildClaimRequest(loss, people, review).policyId).toBeUndefined();
  });

  it('matches reserve amount signs and authority thresholds', () => {
    const review = createReviewForm();
    review.patchValue({ enabled: true, component: 'SubrogationRecoverable', amount: 200, changeReason: 'Recovery' });
    expect(review.invalid).toBeTrue();
    review.controls.amount.setValue(-200);
    expect(review.valid).toBeTrue();
    expect(authorityIndicator(10_000).level).toBe('auto');
    expect(authorityIndicator(10_000.01).level).toBe('supervisor');
    expect(authorityIndicator(100_000.01).level).toBe('manager');
  });

  it('maps server fields to their steps', () => {
    expect(stepForField('LossDate')).toBe(0);
    expect(stepForField('Parties[0].FirstName')).toBe(1);
    expect(stepForField('RiskObjects')).toBe(1);
    expect(stepForField('InitialReserve.Amount')).toBe(2);
  });
});
