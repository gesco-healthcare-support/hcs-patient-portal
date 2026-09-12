import { Injectable } from '@angular/core';
import { NgbDateParserFormatter, NgbDateStruct } from '@ng-bootstrap/ng-bootstrap';

/**
 * QA #15 item 5 (2026-07-07): product-wide US date presentation for every ngb
 * datepicker input. Replaces ABP's culture-driven DateParserFormatter, whose
 * display followed the API host culture's shortDatePattern (the dev containers
 * rendered DD/MM/YYYY) and whose parser only understood dash-separated y-M-d,
 * making typed entry unusable.
 *
 * Format: always MM/DD/YYYY. Parse: M/D/YYYY or MM/DD/YYYY (also - or .
 * separators), validated against the real calendar; anything else returns null,
 * which ng-bootstrap's input directive surfaces as an `ngbDate` control error so
 * form gates (wizard step Continue, injury-modal save) block on bad input.
 */
@Injectable()
export class UsDateParserFormatter extends NgbDateParserFormatter {
  parse(value: string): NgbDateStruct | null {
    if (!value) {
      return null;
    }
    const match = /^\s*(\d{1,2})[/\-.](\d{1,2})[/\-.](\d{4})\s*$/.exec(value);
    if (!match) {
      return null;
    }
    const month = Number(match[1]);
    const day = Number(match[2]);
    const year = Number(match[3]);
    // Round-trip through Date to reject non-calendar combinations (Feb 30,
    // month 13, day 0) instead of letting them roll over to the next month.
    const probe = new Date(year, month - 1, day);
    if (probe.getFullYear() !== year || probe.getMonth() !== month - 1 || probe.getDate() !== day) {
      return null;
    }
    return { year, month, day };
  }

  format(date: NgbDateStruct | null): string {
    if (!date) {
      return '';
    }
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${pad(date.month)}/${pad(date.day)}/${date.year}`;
  }
}
