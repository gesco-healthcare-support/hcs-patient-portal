/**
 * Neutralises a CSV cell that a spreadsheet would otherwise read as a formula.
 *
 * Quoting does not do this. RFC-4180 quotes are removed on import, so a cell holding
 * `=HYPERLINK("http://x","click")` is still evaluated by Excel, LibreOffice and Sheets
 * after the quotes come off. The only thing that stops it is making the cell start with
 * something that is not a formula trigger.
 *
 * Three CSV exports in this product carry values the caller controls:
 *
 * - the Appointment Request Report, built on the server (`AppointmentReportCsv`), whose
 *   patient name, email and phone come from the patient record;
 * - the appointments list export (`toCsvContent`), same patient name, same source;
 * - the audit-log export (`buildAuditCsv`), whose `Client` column is the request's
 *   User-Agent and whose `URL` column is the request URL. **Those are set by whoever
 *   sends the request, including an anonymous one**, which makes that export reachable
 *   without an account at all.
 *
 * The exports are opened by staff, and each lists many rows, so a formula in one row can
 * read its neighbours and carry the result to an external address on a single click.
 */

/** Characters a spreadsheet treats as the start of a formula. */
const FORMULA_TRIGGERS = new Set(['=', '+', '-', '@', '\t', '\r']);

/**
 * Prefixes an apostrophe when the value opens with a formula trigger. Spreadsheets read
 * a leading apostrophe as "the rest of this cell is text" and do not render it in the
 * cell; it shows only in the formula bar.
 *
 * `+` and `-` are on the list even though an international phone number legitimately
 * begins with `+`. The cost is a formula-bar oddity on those numbers; the cost of
 * leaving `+` off is that it is one of the characters Excel reliably accepts as a
 * formula start. The server-side exporter makes the same trade and says so.
 *
 * Call this BEFORE quoting, never after: a neutralised cell that also contains a comma
 * still needs its RFC-4180 quotes, and quoting first would put the apostrophe inside
 * them where it no longer starts the cell.
 */
export function neutraliseCsvCell(value: string | number | null | undefined): string {
  const text = String(value ?? '');
  return text.length > 0 && FORMULA_TRIGGERS.has(text[0]) ? `'${text}` : text;
}
