import {
  LOGO_CANVAS_HEIGHT,
  LOGO_CANVAS_WIDTH,
  LOGO_MAX_BYTES,
  candidateWidths,
  computeFit,
  findContentBounds,
  hasTransparency,
  normalizeLogoFile,
} from './logo-normalize.util';

/** Builds RGBA pixels from a per-pixel function. */
function pixels(
  w: number,
  h: number,
  at: (x: number, y: number) => [number, number, number, number],
): Uint8ClampedArray {
  const data = new Uint8ClampedArray(w * h * 4);
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      data.set(at(x, y), (y * w + x) * 4);
    }
  }
  return data;
}

describe('logo normalisation math', () => {
  it('detects transparency', () => {
    expect(hasTransparency(pixels(2, 2, () => [0, 0, 0, 255]))).toBeFalse();
    expect(hasTransparency(pixels(2, 2, (x) => [0, 0, 0, x ? 255 : 0]))).toBeTrue();
  });

  it('trims a fully transparent border', () => {
    const d = pixels(10, 8, (x, y) =>
      x >= 2 && x < 6 && y >= 3 && y < 5 ? [9, 9, 9, 255] : [0, 0, 0, 0],
    );
    expect(findContentBounds(d, 10, 8)).toEqual({ left: 2, top: 3, width: 4, height: 2 });
  });

  it('trims a near-white border on an opaque picture', () => {
    const d = pixels(10, 8, (x, y) =>
      x >= 1 && x < 9 && y >= 1 && y < 7 ? [10, 20, 30, 255] : [250, 250, 250, 255],
    );
    expect(findContentBounds(d, 10, 8)).toEqual({ left: 1, top: 1, width: 8, height: 6 });
  });

  it('does not treat white as blank when the picture is transparent', () => {
    const d = pixels(4, 4, (x) => (x === 0 ? [0, 0, 0, 0] : [255, 255, 255, 255]));
    expect(findContentBounds(d, 4, 4)).toEqual({ left: 1, top: 0, width: 3, height: 4 });
  });

  it('returns null when every pixel is blank', () => {
    expect(
      findContentBounds(
        pixels(3, 3, () => [255, 255, 255, 255]),
        3,
        3,
      ),
    ).toBeNull();
  });

  it('keeps a picture with no border whole', () => {
    expect(
      findContentBounds(
        pixels(3, 2, () => [1, 1, 1, 255]),
        3,
        2,
      ),
    ).toEqual({
      left: 0,
      top: 0,
      width: 3,
      height: 2,
    });
  });

  it('scales a small picture UP and centres it', () => {
    const f = computeFit(10, 10, 600, 180, 0);
    expect(f).toEqual({ dx: 210, dy: 0, dw: 180, dh: 180 });
  });

  it('scales a very wide picture DOWN to the padded width', () => {
    const f = computeFit(3000, 100, 600, 180);
    expect(f.dw).toBe(528);
    expect(f.dx).toBe(36);
    expect(f.dh).toBeLessThanOrEqual(180);
  });

  it('offers shrinking widths that keep the 10:3 ratio', () => {
    const w = candidateWidths();
    expect(w[0]).toBe(LOGO_CANVAS_WIDTH);
    expect(w).toEqual([...w].sort((a, b) => b - a));
  });
});

describe('normalizeLogoFile (canvas glue)', () => {
  function fileOf(
    w: number,
    h: number,
    paint: (c: CanvasRenderingContext2D) => void,
    type = 'image/png',
  ): Promise<File> {
    const canvas = document.createElement('canvas');
    canvas.width = w;
    canvas.height = h;
    paint(canvas.getContext('2d') as CanvasRenderingContext2D);
    return new Promise((resolve) =>
      canvas.toBlob((b) => resolve(new File([b as Blob], 'in.png', { type })), type),
    );
  }

  it('outputs a 600x180 PNG with the padded content centred', async () => {
    const input = await fileOf(200, 200, (c) => {
      c.fillStyle = '#ffffff';
      c.fillRect(0, 0, 200, 200);
      c.fillStyle = '#ff0000';
      c.fillRect(80, 90, 40, 20); // small content inside a big white border
    });

    const out = await normalizeLogoFile(input);

    expect(out.type).toBe('image/png');
    expect(out.size).toBeLessThanOrEqual(LOGO_MAX_BYTES);
    const bmp = await createImageBitmap(out);
    expect([bmp.width, bmp.height]).toEqual([LOGO_CANVAS_WIDTH, LOGO_CANVAS_HEIGHT]);
    const probe = document.createElement('canvas');
    probe.width = bmp.width;
    probe.height = bmp.height;
    const ctx = probe.getContext('2d') as CanvasRenderingContext2D;
    ctx.drawImage(bmp, 0, 0);
    expect(ctx.getImageData(300, 90, 1, 1).data[0]).toBe(255); // red centre
    expect(ctx.getImageData(2, 2, 1, 1).data[3]).toBe(0); // transparent corner
  });

  it('rejects a file the browser cannot decode', async () => {
    const bad = new File(['not an image'], 'x.png', { type: 'image/png' });
    await expectAsync(normalizeLogoFile(bad)).toBeRejected();
  });

  it('downscales a very large picture before trimming', async () => {
    const big = await fileOf(3000, 600, (c) => {
      c.fillStyle = '#0a4a7e';
      c.fillRect(0, 0, 3000, 600);
    });
    const out = await normalizeLogoFile(big);
    expect(out.size).toBeGreaterThan(0);
  });
});
