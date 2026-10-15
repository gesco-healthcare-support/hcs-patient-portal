import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpHeaders, HttpResponse } from '@angular/common/http';
import { of } from 'rxjs';
import { PermissionService, RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { AppointmentPacketComponent } from './appointment-packet.component';
import { AppointmentPacketService } from '../proxy/appointment-documents/appointment-packet.service';
import { AppointmentDocumentService } from '../proxy/appointment-documents/appointment-document.service';
import { AppointmentDocumentUrls } from '../appointment-documents/appointment-document-urls';
import { PacketKind } from '../proxy/appointment-documents/packet-kind.enum';
import { PacketGenerationStatus } from '../proxy/appointment-documents/packet-generation-status.enum';

/**
 * #628 changed two things in this component's download path and one in its
 * change detection, none of which had any coverage.
 *
 *  - S6535 removed two redundant escapes from the content-disposition filename
 *    regex. The patterns are provably equivalent, but "provably" is worth
 *    little without the header shapes actually being run through it.
 *  - S7762 replaced document.body.removeChild(anchor) with anchor.remove().
 *  - S1871 merged two identical ngOnChanges arms into one condition.
 *
 * The filename cases below are the ones a real server sends: bare, quoted,
 * RFC-5987 UTF-8, and absent. The quoted-with-semicolon case is included
 * because it is the one where the character class actually does work.
 */
describe('AppointmentPacketComponent download and change detection (#628)', () => {
  interface Probe {
    appointmentId: string | null;
    expectPackets: boolean;
    zeroRowPolls: number;
    refresh(): void;
    downloadInternal(packet: unknown): Promise<void>;
    ngOnChanges(changes: Record<string, unknown>): void;
  }

  let anchors: HTMLAnchorElement[];
  let created: string[];

  function create(contentDisposition: string | null): Probe {
    anchors = [];
    created = [];
    const headers = contentDisposition
      ? new HttpHeaders({ 'content-disposition': contentDisposition })
      : new HttpHeaders();

    TestBed.configureTestingModule({
      providers: [
        {
          provide: HttpClient,
          useValue: {
            get: () => of(new HttpResponse({ body: new Blob(['pdf']), headers, status: 200 })),
          },
        },
        { provide: AppointmentPacketService, useValue: { getList: () => of({ items: [] }) } },
        { provide: AppointmentDocumentService, useValue: { getList: () => of({ items: [] }) } },
        { provide: AppointmentDocumentUrls, useValue: { buildPacket: () => '/packet' } },
        { provide: RestService, useValue: { request: () => of(null) } },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
      ],
    });

    // Capture the synthetic anchor so both the filename and the cleanup can be
    // asserted; the real click would ask the browser to download.
    const realCreate = document.createElement.bind(document);
    spyOn(document, 'createElement').and.callFake((tag: string) => {
      const el = realCreate(tag);
      if (tag === 'a') {
        const a = el as HTMLAnchorElement;
        spyOn(a, 'click').and.stub();
        anchors.push(a);
      }
      return el;
    });
    spyOn(URL, 'createObjectURL').and.callFake(() => {
      const u = 'blob:test-' + created.length;
      created.push(u);
      return u;
    });
    spyOn(URL, 'revokeObjectURL').and.stub();

    const c = TestBed.runInInjectionContext(
      () => new AppointmentPacketComponent(),
    ) as unknown as Probe;
    c.appointmentId = 'appt-1';
    return c;
  }

  const packet = {
    kind: PacketKind.Patient,
    status: PacketGenerationStatus.Generated,
  };

  afterEach(() => TestBed.resetTestingModule());

  const CASES: Array<[string, string | null, string]> = [
    ['a bare filename', 'attachment; filename=doctor.pdf', 'doctor.pdf'],
    ['a quoted filename', 'attachment; filename="doctor packet.pdf"', 'doctor packet.pdf'],
    [
      'an RFC-5987 UTF-8 filename',
      "attachment; filename*=UTF-8''doctor%20packet.pdf",
      'doctor packet.pdf',
    ],
    ['a quoted filename containing a semicolon', 'attachment; filename="a;b.pdf"; size=3', 'a'],
  ];

  for (const [label, header, expected] of CASES) {
    it(`takes the download name from ${label}`, async () => {
      const c = create(header);
      await c.downloadInternal(packet);
      expect(anchors).toHaveSize(1);
      expect(anchors[0].download).toBe(expected);
    });
  }

  it('falls back to a synthesised name when the header has none', async () => {
    const c = create('inline');
    await c.downloadInternal(packet);
    expect(anchors[0].download).toBe('Patient.pdf');
  });

  /** S7762: the anchor must leave the document whichever way it is removed. */
  it('removes the synthetic anchor from the document again', async () => {
    const c = create('attachment; filename=doctor.pdf');
    await c.downloadInternal(packet);
    expect(anchors[0].isConnected).toBeFalse();
    expect(document.querySelector('a[download]')).toBeNull();
  });

  it('does nothing for a packet that is not generated', async () => {
    const c = create('attachment; filename=doctor.pdf');
    await c.downloadInternal({ kind: PacketKind.Patient, status: PacketGenerationStatus.Failed });
    expect(anchors).toHaveSize(0);
  });

  describe('ngOnChanges (S1871: two triggers, one response)', () => {
    it('refreshes and resets the poll budget when the appointment changes', () => {
      const c = create(null);
      spyOn(c, 'refresh');
      c.zeroRowPolls = 4;

      c.ngOnChanges({ appointmentId: {} });

      expect(c.zeroRowPolls).toBe(0);
      expect(c.refresh).toHaveBeenCalledTimes(1);
    });

    it('does the same when the status flips to Approved', () => {
      const c = create(null);
      spyOn(c, 'refresh');
      c.zeroRowPolls = 4;
      c.expectPackets = true;

      c.ngOnChanges({ expectPackets: {} });

      expect(c.zeroRowPolls).toBe(0);
      expect(c.refresh).toHaveBeenCalledTimes(1);
    });

    /** The guard that stops the merged condition firing on every change. */
    it('ignores an expectPackets change that turns it off', () => {
      const c = create(null);
      spyOn(c, 'refresh');
      c.zeroRowPolls = 4;
      c.expectPackets = false;

      c.ngOnChanges({ expectPackets: {} });

      expect(c.zeroRowPolls).toBe(4);
      expect(c.refresh).not.toHaveBeenCalled();
    });
  });
});
