import { TestBed } from '@angular/core/testing';
import { FileDropZoneDirective } from './file-drop-zone.directive';

/**
 * The drop zone shared by the three document uploaders.
 *
 * The one behaviour that loses data if it regresses is preventDefault on dragover: without it the
 * browser navigates to the dropped file and whatever the user had filled in is gone. So that call is
 * asserted directly, and so is its absence while the zone is disabled -- a disabled zone must hand
 * the event back to the browser untouched.
 *
 * Built in an injection context (not rendered) because `output()` needs one and nothing here is
 * template-bound.
 */
describe('FileDropZoneDirective', () => {
  const pdf = new File(['%PDF'], 'report.pdf', { type: 'application/pdf' });

  function directive(): FileDropZoneDirective {
    return TestBed.runInInjectionContext(() => new FileDropZoneDirective());
  }

  function dragEvent(files: File[] | null = []) {
    return {
      preventDefault: jasmine.createSpy('preventDefault'),
      stopPropagation: jasmine.createSpy('stopPropagation'),
      dataTransfer: files === null ? null : { files },
    } as unknown as DragEvent & { preventDefault: jasmine.Spy; stopPropagation: jasmine.Spy };
  }

  function emitted(dir: FileDropZoneDirective): jasmine.Spy {
    const spy = jasmine.createSpy('filesDropped');
    dir.filesDropped.subscribe(spy);
    return spy;
  }

  it('claims a dragover so the browser does not open the file, and shows the drop state', () => {
    const dir = directive();
    const event = dragEvent();

    dir.onDragOver(event);

    expect(event.preventDefault).toHaveBeenCalled();
    expect(event.stopPropagation).toHaveBeenCalled();
    expect(dir.isDragOver).toBeTrue();
  });

  it('hands a dragover back to the browser untouched while disabled', () => {
    const dir = directive();
    dir.dropDisabled = true;
    const event = dragEvent();

    dir.onDragOver(event);

    expect(event.preventDefault).not.toHaveBeenCalled();
    expect(dir.isDragOver).toBeFalse();
  });

  it('clears the drop state when the drag leaves', () => {
    const dir = directive();
    dir.onDragOver(dragEvent());

    dir.onDragLeave(dragEvent());

    expect(dir.isDragOver).toBeFalse();
  });

  it('emits the dropped files and clears the drop state', () => {
    const dir = directive();
    const spy = emitted(dir);
    dir.onDragOver(dragEvent());

    dir.onDrop(dragEvent([pdf]));

    expect(spy).toHaveBeenCalledOnceWith([pdf]);
    expect(dir.isDragOver).toBeFalse();
  });

  it('emits nothing while disabled', () => {
    const dir = directive();
    dir.dropDisabled = true;
    const spy = emitted(dir);

    dir.onDrop(dragEvent([pdf]));

    expect(spy).not.toHaveBeenCalled();
  });

  it('emits nothing when the drop carried no files', () => {
    const dir = directive();
    const spy = emitted(dir);

    dir.onDrop(dragEvent([]));
    dir.onDrop(dragEvent(null));

    expect(spy).not.toHaveBeenCalled();
  });
});
