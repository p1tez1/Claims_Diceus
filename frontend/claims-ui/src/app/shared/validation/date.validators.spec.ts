import { FormControl, FormGroup } from '@angular/forms';
import { dateRangeOrderValidator, userDateRangeValidator } from './date.validators';

describe('shared date validators', () => {
  it('accepts empty and valid dates', () => {
    const control = new FormControl('', userDateRangeValidator);
    expect(control.valid).toBeTrue();
    control.setValue('2026-02-22');
    expect(control.valid).toBeTrue();
  });

  it('rejects years outside the supported four-digit range', () => {
    const control = new FormControl('222222-02-22', userDateRangeValidator);
    expect(control.hasError('dateRange')).toBeTrue();
    control.setValue('1899-12-31');
    expect(control.hasError('dateRange')).toBeTrue();
    control.setValue('2101-01-01');
    expect(control.hasError('dateRange')).toBeTrue();
  });

  it('rejects a from date after the to date and permits empty bounds', () => {
    const range = new FormGroup({ from: new FormControl('2026-02-23'), to: new FormControl('2026-02-22') },
      dateRangeOrderValidator('from', 'to'));
    expect(range.hasError('dateRangeOrder')).toBeTrue();
    range.controls.from.setValue('');
    expect(range.valid).toBeTrue();
  });
});
