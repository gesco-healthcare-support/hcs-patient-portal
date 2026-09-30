import {
  AppointmentAddDocumentsComponent,
  OTHER_DOCUMENT_TYPE_MAX_LENGTH,
  OTHER_DOCUMENT_TYPE_VALUE,
  StagedDocumentUpload,
} from './appointment-add-documents.component';

/**
 * The booking form's pre-submit documents card. It holds no state of its own: it emits picks,
 * drops, removals and per-row choices, and the parent stages and uploads.
 *
 * <p>It had no spec. It injects nothing, so it is built with `new`, and each handler is driven
 * with the minimal event shape it reads.</p>
 *
 * <p>All file names below are synthetic.</p>
 */
describe('AppointmentAddDocumentsComponent', () => {
  const file = (name: string) => new File(['x'], name, { type: 'application/pdf' });

  function staged(status: StagedDocumentUpload['status']): StagedDocumentUpload {
    return { file: file('scan.pdf'), status, isStrikeList: false };
  }

  function emitted<T>(emitter: { subscribe(fn: (v: T) => void): unknown }): T[] {
    const out: T[] = [];
    emitter.subscribe((v) => out.push(v));
    return out;
  }

  it('starts empty and enabled, and exposes the "Other" option and its cap', () => {
    const c = new AppointmentAddDocumentsComponent() as unknown as Record<string, unknown>;
    expect(c['stagedDocuments']).toEqual([]);
    expect(c['disabled']).toBeFalse();
    expect(c['otherValue']).toBe(OTHER_DOCUMENT_TYPE_VALUE);
    expect(c['otherMaxLength']).toBe(OTHER_DOCUMENT_TYPE_MAX_LENGTH);
  });

  describe('per-row and card choices', () => {
    it('raises the strike-list checkbox state', () => {
      const c = new AppointmentAddDocumentsComponent();
      const out = emitted<boolean>(c.hasPanelStrikeListChange);
      c.onHasPanelStrikeListChange({ target: { checked: true } } as unknown as Event);
      expect(out).toEqual([true]);
    });

    it('raises the chosen document type, and null for the unspecified option', () => {
      const c = new AppointmentAddDocumentsComponent();
      const out = emitted<{ index: number; typeId: string | null }>(c.documentTypeChange);

      c.onDocumentTypeChange(2, { target: { value: 'type-1' } } as unknown as Event);
      c.onDocumentTypeChange(0, { target: { value: '' } } as unknown as Event);

      expect(out).toEqual([
        { index: 2, typeId: 'type-1' },
        { index: 0, typeId: null },
      ]);
    });

    it('raises the typed "Other" label for its row', () => {
      const c = new AppointmentAddDocumentsComponent();
      const out = emitted<{ index: number; value: string }>(c.otherDocumentTypeNameChange);
      c.onOtherDocumentTypeNameChange(1, { target: { value: 'Imaging CD' } } as unknown as Event);
      expect(out).toEqual([{ index: 1, value: 'Imaging CD' }]);
    });
  });

  it('reports a failed upload only when one of the staged files failed', () => {
    const c = new AppointmentAddDocumentsComponent();
    c.stagedDocuments = [staged('uploaded'), staged('staged')];
    expect(c.hasFailedUploads).toBeFalse();

    c.stagedDocuments = [staged('uploaded'), staged('failed')];
    expect(c.hasFailedUploads).toBeTrue();
  });

  describe('picking files', () => {
    it('raises the picked files and clears the input so the same file can be picked again', () => {
      const c = new AppointmentAddDocumentsComponent();
      const out = emitted<File[]>(c.filesSelected);
      const picked = [file('a.pdf'), file('b.pdf')];
      const input = { files: picked, value: 'C:\\fakepath\\a.pdf' };

      c.onFilesSelected({ target: input } as unknown as Event);

      expect(out).toEqual([picked]);
      expect(input.value).toBe('');
    });

    it('ignores a pick that carries no files', () => {
      const c = new AppointmentAddDocumentsComponent();
      const out = emitted<File[]>(c.filesSelected);

      c.onFilesSelected({ target: { files: null, value: '' } } as unknown as Event);
      c.onFilesSelected({ target: { files: [], value: '' } } as unknown as Event);

      expect(out).toEqual([]);
    });
  });

  describe('dropping files', () => {
    it('raises dropped files through the same output as picked ones', () => {
      const c = new AppointmentAddDocumentsComponent();
      const out = emitted<File[]>(c.filesSelected);
      const dropped = [file('drop.pdf')];

      c.onFilesDropped(dropped);

      expect(out).toEqual([dropped]);
    });

    it('ignores a drop while the card is locked, and a drop with no files', () => {
      const c = new AppointmentAddDocumentsComponent();
      const out = emitted<File[]>(c.filesSelected);

      c.disabled = true;
      c.onFilesDropped([file('drop.pdf')]);
      c.disabled = false;
      c.onFilesDropped([]);

      expect(out).toEqual([]);
    });
  });

  it('shows sizes below a mebibyte in KB and the rest in MB', () => {
    const c = new AppointmentAddDocumentsComponent();
    expect(c.formatBytes(1536)).toBe('1.5 KB');
    expect(c.formatBytes(1024 * 1024 - 1)).toBe('1024.0 KB');
    expect(c.formatBytes(1024 * 1024)).toBe('1.0 MB');
    expect(c.formatBytes(5.25 * 1024 * 1024)).toBe('5.3 MB');
  });
});
