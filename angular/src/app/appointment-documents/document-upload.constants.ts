/**
 * AF7 / BUG-025 (2026-06-05): client mirror of the authoritative server upload
 * cap (`AppointmentDocumentConsts.MaxFileSizeBytes` = 10 MB). Shared by the
 * appointment-view uploader and the booking-form pre-submit picker so the
 * friendly "too large" message fires client-side before the 12 MB
 * Kestrel/multipart framework cap returns a raw 413, and so the client cap
 * cannot drift from the server limit.
 */
export const MAX_DOCUMENT_UPLOAD_BYTES = 10 * 1024 * 1024;

/**
 * Allowed upload extensions. Mirrors the server `EnsureValidFileFormat`
 * allowlist (`AppointmentDocumentsAppService`) so the picker rejects unsupported
 * files before any round-trip.
 */
export const ALLOWED_DOCUMENT_EXTENSIONS: readonly string[] = [
  '.pdf',
  '.jpg',
  '.jpeg',
  '.png',
  // Item G (2026-08-22): Word, as .docx ONLY. Not .docm (macro-enabled by definition) and not .doc
  // (legacy OLE binary). This list is a convenience so the picker can reject early; the server is
  // still authoritative and additionally opens the .docx container to confirm it really is a Word
  // document with no vbaProject.bin, which no extension check can establish.
  '.docx',
];
