import { Pipe, PipeTransform } from '@angular/core';

export type AuthorityLevel = 'auto' | 'supervisor' | 'manager';
export interface AuthorityIndicator { level: AuthorityLevel; label: string; }

export function authorityIndicator(amount: number): AuthorityIndicator {
  const absoluteAmount = Math.abs(amount);
  if (absoluteAmount <= 10_000) return { level: 'auto', label: 'Auto-approved (≤ $10,000)' };
  if (absoluteAmount <= 100_000) return { level: 'supervisor', label: 'Supervisor approval required' };
  return { level: 'manager', label: 'Manager approval required' };
}

@Pipe({ name: 'authorityIndicator', standalone: true, pure: true })
export class AuthorityIndicatorPipe implements PipeTransform {
  transform(amount: number): string { return authorityIndicator(amount).label; }
}
