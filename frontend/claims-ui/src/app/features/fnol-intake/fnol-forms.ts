import { AbstractControl, FormControl, FormGroup, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { CreateClaimRequest, PartyInput, ReserveComponentType, RiskObjectInput } from '../../core/models/api.models';
import { parseStrictDateInput, userDateRangeValidator } from '../../shared/validation/date.validators';

export const futureDate: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = control.value as string;
  if (!value) return null;
  const date = parseStrictDateInput(value);
  return date && date.getTime() > Date.now() ? { futureDate: true } : null;
};
export const minDescription: ValidatorFn = (control: AbstractControl): ValidationErrors | null =>
  (control.value as string).trim().length >= 20 ? null : { minDescription: true };
export const nonBlank: ValidatorFn = (control: AbstractControl): ValidationErrors | null =>
  (control.value as string).trim().length > 0 ? null : { required: true };
export const claimantRequired: ValidatorFn = (control: AbstractControl): ValidationErrors | null =>
  (control.value as PartyInput[]).some(party => party.role === 'Claimant') ? null : { claimantRequired: true };
export const policyRequired: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = control.value as { policyId: string; unknownPolicy: boolean };
  return value.unknownPolicy || value.policyId ? null : { policyRequired: true };
};
export const reserveComplete: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = control.value as { enabled: boolean; component: ReserveComponentType; amount: number | null; changeReason: string };
  if (!value.enabled) return null;
  if (!value.component || value.amount === null || !value.changeReason.trim()) return { reserveComplete: true };
  if (value.component === 'SubrogationRecoverable' ? value.amount >= 0 : value.amount <= 0) return { reserveComplete: true };
  return null;
};

export function createLossForm() {
  return new FormGroup({
    policySearch: new FormControl('', { nonNullable: true }),
    policyId: new FormControl('', { nonNullable: true }),
    unknownPolicy: new FormControl(false, { nonNullable: true }),
    lossDate: new FormControl('', { nonNullable: true, validators: [Validators.required, userDateRangeValidator, futureDate] }),
    causeSearch: new FormControl('', { nonNullable: true }),
    causeOfLossCode: new FormControl('', { nonNullable: true, validators: Validators.required }),
    lossDescription: new FormControl('', { nonNullable: true, validators: [Validators.required, minDescription] }),
    lossLocation: new FormControl('', { nonNullable: true, validators: Validators.maxLength(500) }),
    estimatedLossAmount: new FormControl<number | null>(null, { validators: Validators.min(0) })
  }, { validators: policyRequired });
}
export function createPartiesForm() {
  return new FormGroup({
    parties: new FormControl<PartyInput[]>([], { nonNullable: true, validators: claimantRequired }),
    riskObjects: new FormControl<RiskObjectInput[]>([], { nonNullable: true })
  });
}
export function createReviewForm() {
  return new FormGroup({
    enabled: new FormControl(false, { nonNullable: true }),
    component: new FormControl<ReserveComponentType>('Indemnity', { nonNullable: true }),
    amount: new FormControl<number | null>(null),
    changeReason: new FormControl('', { nonNullable: true, validators: Validators.maxLength(500) })
  }, { validators: reserveComplete });
}
export type LossForm = ReturnType<typeof createLossForm>;
export type PartiesForm = ReturnType<typeof createPartiesForm>;
export type ReviewForm = ReturnType<typeof createReviewForm>;

export function buildClaimRequest(loss: LossForm, people: PartiesForm, review: ReviewForm): CreateClaimRequest {
  const l = loss.getRawValue(), p = people.getRawValue(), r = review.getRawValue();
  return {
    lossDate: new Date(l.lossDate).toISOString(), lossDescription: l.lossDescription.trim(),
    causeOfLossCode: l.causeOfLossCode, parties: p.parties, riskObjects: p.riskObjects,
    ...(!l.unknownPolicy && l.policyId ? { policyId: l.policyId } : {}),
    ...(l.lossLocation.trim() ? { lossLocation: l.lossLocation.trim() } : {}),
    ...(l.estimatedLossAmount !== null ? { estimatedLossAmount: l.estimatedLossAmount } : {}),
    ...(r.enabled && !l.unknownPolicy ? { initialReserve: { component: r.component, amount: r.amount!, changeReason: r.changeReason.trim() } } : {})
  };
}

export function inForce(lossDate: string, effectiveDate: string, expirationDate: string): boolean {
  if (!lossDate || !Number.isFinite(Date.parse(lossDate))) return false;
  const day = new Date(lossDate).toISOString().slice(0, 10);
  return day >= effectiveDate.slice(0, 10) && day <= expirationDate.slice(0, 10);
}

export function stepForField(field: string): 0 | 1 | 2 {
  if (/^(Parties|RiskObjects)/i.test(field)) return 1;
  if (/^(InitialReserve|Component|Amount|ChangeReason)/i.test(field)) return 2;
  return 0;
}
