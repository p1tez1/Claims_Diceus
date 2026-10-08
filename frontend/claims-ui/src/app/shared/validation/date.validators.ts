import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export const MIN_USER_DATE = '1900-01-01';
export const MAX_USER_DATE = '2100-12-31';

const strictDatePattern = /^(\d{4})-(\d{2})-(\d{2})(?:T(\d{2}):(\d{2})(?::(\d{2})(?:\.(\d{1,3}))?)?)?$/;

export function parseStrictDateInput(value: string): Date | null {
  const match = strictDatePattern.exec(value);
  if (!match) return null;
  const [, yearText, monthText, dayText, hourText = '0', minuteText = '0', secondText = '0', millisecondText = '0'] = match;
  const year = Number(yearText), month = Number(monthText), day = Number(dayText);
  const hour = Number(hourText), minute = Number(minuteText), second = Number(secondText);
  const millisecond = Number(millisecondText.padEnd(3, '0'));
  if (year < 1900 || year > 2100 || month < 1 || month > 12 || day < 1 || day > 31 ||
      hour > 23 || minute > 59 || second > 59) return null;
  const date = new Date(year, month - 1, day, hour, minute, second, millisecond);
  return date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day &&
    date.getHours() === hour && date.getMinutes() === minute && date.getSeconds() === second ? date : null;
}

export const userDateRangeValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = control.value as string;
  return !value || parseStrictDateInput(value) ? null : { dateRange: true };
};

export function dateRangeOrderValidator(fromControl: string, toControl: string): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const from = control.get(fromControl)?.value as string | undefined;
    const to = control.get(toControl)?.value as string | undefined;
    if (!from || !to || !parseStrictDateInput(from) || !parseStrictDateInput(to)) return null;
    return from > to ? { dateRangeOrder: true } : null;
  };
}

export function localDateTimeInputValue(date: Date): string {
  const part = (value: number) => value.toString().padStart(2, '0');
  return `${date.getFullYear()}-${part(date.getMonth() + 1)}-${part(date.getDate())}T${part(date.getHours())}:${part(date.getMinutes())}`;
}
