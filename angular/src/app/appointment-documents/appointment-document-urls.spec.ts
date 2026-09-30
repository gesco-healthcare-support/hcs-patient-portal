import { TestBed } from '@angular/core/testing';
import { EnvironmentService } from '@abp/ng.core';
import { PacketKind } from '../proxy/appointment-documents/packet-kind.enum';
import { AppointmentDocumentUrls } from './appointment-document-urls';

/**
 * Download URLs for documents and rendered packets. The consumer opens these directly, so a wrong
 * segment is a dead link rather than an error anyone sees.
 *
 * The stubbed environment answers a DIFFERENT base for any api name other than 'Default', so these
 * also pin which API the downloads are served from.
 */
describe('AppointmentDocumentUrls', () => {
  function urls(defaultApi: string | undefined): AppointmentDocumentUrls {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: EnvironmentService,
          useValue: {
            getApiUrl: (name: string) =>
              name === 'Default' ? defaultApi : 'https://wrong-api.example.test',
          },
        },
      ],
    });
    return TestBed.inject(AppointmentDocumentUrls);
  }

  afterEach(() => TestBed.resetTestingModule());

  it('builds the single-document download URL on the Default API', () => {
    expect(urls('https://api.example.test').build('appt-1', 'doc-1')).toBe(
      'https://api.example.test/api/app/appointments/appt-1/documents/doc-1/download',
    );
  });

  it('builds the per-kind packet download URL', () => {
    expect(urls('https://api.example.test').buildPacket('appt-1', PacketKind.Doctor)).toBe(
      'https://api.example.test/api/app/appointments/appt-1/packet/download/2',
    );
  });

  it('falls back to a relative URL when no API base is configured', () => {
    const u = urls(undefined);

    expect(u.build('appt-1', 'doc-1')).toBe(
      '/api/app/appointments/appt-1/documents/doc-1/download',
    );
    expect(u.buildPacket('appt-1', PacketKind.Patient)).toBe(
      '/api/app/appointments/appt-1/packet/download/1',
    );
  });
});
