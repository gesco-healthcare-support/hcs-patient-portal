/**
 * Logo normalisation (Q13). Runs in the browser BEFORE upload so every office logo reaches the
 * server already trimmed and sitting in one canonical canvas; the server stores it untouched
 * (it deliberately does no image decoding, see Directory.Build.props).
 *
 * The pure math (trim bounds, fit) is separate from the thin canvas glue so it can be tested
 * on plain pixel arrays.
 */

/** Canonical logo canvas: 10:3, wide enough for horizontal marks, small as a PNG. */
export const LOGO_CANVAS_WIDTH = 600;
export const LOGO_CANVAS_HEIGHT = 180;
/** Server cap is 1 MB; stay a little under so a multipart envelope never tips it over. */
export const LOGO_MAX_BYTES = 1_000_000;
/** Largest source edge decoded for trimming; bigger pictures are scaled down first. */
export const LOGO_MAX_DECODE_EDGE = 2000;

const ALPHA_BLANK = 8; // alpha at or below this counts as transparent
const ALPHA_OPAQUE = 250; // alpha at or above this counts as opaque
const WHITE_BLANK = 245; // every channel at or above this counts as near-white
const CANVAS_PADDING_RATIO = 0.06; // transparent margin kept inside the canvas

export interface Bounds {
  left: number;
  top: number;
  width: number;
  height: number;
}

export interface FitRect {
  dx: number;
  dy: number;
  dw: number;
  dh: number;
}

/** True when any pixel is meaningfully transparent (so blankness is judged by alpha, not colour). */
export function hasTransparency(data: Uint8ClampedArray): boolean {
  for (let i = 3; i < data.length; i += 4) {
    if (data[i] < ALPHA_OPAQUE) {
      return true;
    }
  }
  return false;
}

function isBlankPixel(data: Uint8ClampedArray, i: number, byAlpha: boolean): boolean {
  if (byAlpha) {
    return data[i + 3] <= ALPHA_BLANK;
  }
  return data[i] >= WHITE_BLANK && data[i + 1] >= WHITE_BLANK && data[i + 2] >= WHITE_BLANK;
}

function rowIsBlank(data: Uint8ClampedArray, width: number, y: number, byAlpha: boolean): boolean {
  for (let x = 0; x < width; x++) {
    if (!isBlankPixel(data, (y * width + x) * 4, byAlpha)) {
      return false;
    }
  }
  return true;
}

function columnIsBlank(
  data: Uint8ClampedArray,
  width: number,
  height: number,
  x: number,
  byAlpha: boolean,
): boolean {
  for (let y = 0; y < height; y++) {
    if (!isBlankPixel(data, (y * width + x) * 4, byAlpha)) {
      return false;
    }
  }
  return true;
}

/**
 * Smallest rectangle containing every non-blank pixel, or null when the whole picture is blank
 * (callers then keep the full picture). Transparent pictures are trimmed by alpha; opaque ones
 * by near-white, so a white-background JPEG loses its white border.
 */
export function findContentBounds(
  data: Uint8ClampedArray,
  width: number,
  height: number,
): Bounds | null {
  const byAlpha = hasTransparency(data);
  let top = 0;
  while (top < height && rowIsBlank(data, width, top, byAlpha)) {
    top++;
  }
  if (top === height) {
    return null;
  }
  let bottom = height - 1;
  while (bottom > top && rowIsBlank(data, width, bottom, byAlpha)) {
    bottom--;
  }
  let left = 0;
  while (left < width && columnIsBlank(data, width, height, left, byAlpha)) {
    left++;
  }
  let right = width - 1;
  while (right > left && columnIsBlank(data, width, height, right, byAlpha)) {
    right--;
  }
  return { left, top, width: right - left + 1, height: bottom - top + 1 };
}

/** Largest centred rectangle of the source aspect that fits the canvas minus padding (up or down). */
export function computeFit(
  srcWidth: number,
  srcHeight: number,
  canvasWidth: number,
  canvasHeight: number,
  paddingRatio: number = CANVAS_PADDING_RATIO,
): FitRect {
  const availW = canvasWidth * (1 - 2 * paddingRatio);
  const availH = canvasHeight * (1 - 2 * paddingRatio);
  const scale = Math.min(availW / srcWidth, availH / srcHeight);
  const dw = Math.max(1, Math.round(srcWidth * scale));
  const dh = Math.max(1, Math.round(srcHeight * scale));
  return {
    dx: Math.round((canvasWidth - dw) / 2),
    dy: Math.round((canvasHeight - dh) / 2),
    dw,
    dh,
  };
}

/** Canvas widths to try, largest first, until the PNG fits under the byte cap. */
export function candidateWidths(): number[] {
  return [LOGO_CANVAS_WIDTH, 450, 300, 150];
}

function makeCanvas(width: number, height: number): HTMLCanvasElement {
  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  return canvas;
}

function context2d(canvas: HTMLCanvasElement): CanvasRenderingContext2D {
  const ctx = canvas.getContext('2d', { willReadFrequently: true });
  if (!ctx) {
    throw new Error('Canvas is not available in this browser.');
  }
  return ctx;
}

function toPngBlob(canvas: HTMLCanvasElement): Promise<Blob> {
  return new Promise((resolve, reject) => {
    canvas.toBlob(
      (blob) => (blob ? resolve(blob) : reject(new Error('Could not encode the logo.'))),
      'image/png',
    );
  });
}

/** Decodes the picture, scaled so neither edge exceeds LOGO_MAX_DECODE_EDGE, onto a canvas. */
async function decodeToCanvas(file: Blob): Promise<HTMLCanvasElement> {
  const bitmap = await createImageBitmap(file);
  try {
    const shrink = Math.min(1, LOGO_MAX_DECODE_EDGE / Math.max(bitmap.width, bitmap.height));
    const w = Math.max(1, Math.round(bitmap.width * shrink));
    const h = Math.max(1, Math.round(bitmap.height * shrink));
    const canvas = makeCanvas(w, h);
    context2d(canvas).drawImage(bitmap, 0, 0, w, h);
    return canvas;
  } finally {
    bitmap.close();
  }
}

async function renderCanonical(
  source: HTMLCanvasElement,
  bounds: Bounds,
  width: number,
): Promise<Blob> {
  const height = Math.round((width * LOGO_CANVAS_HEIGHT) / LOGO_CANVAS_WIDTH);
  const out = makeCanvas(width, height);
  const ctx = context2d(out);
  const fit = computeFit(bounds.width, bounds.height, width, height);
  ctx.imageSmoothingEnabled = true;
  ctx.imageSmoothingQuality = 'high';
  ctx.drawImage(
    source,
    bounds.left,
    bounds.top,
    bounds.width,
    bounds.height,
    fit.dx,
    fit.dy,
    fit.dw,
    fit.dh,
  );
  return toPngBlob(out);
}

/**
 * Trims the uniform border, fits the logo into the canonical canvas on a transparent
 * background and returns it as a PNG File guaranteed to be under LOGO_MAX_BYTES (or throws).
 */
export async function normalizeLogoFile(file: File): Promise<File> {
  const source = await decodeToCanvas(file);
  const pixels = context2d(source).getImageData(0, 0, source.width, source.height);
  const bounds = findContentBounds(pixels.data, source.width, source.height) ?? {
    left: 0,
    top: 0,
    width: source.width,
    height: source.height,
  };
  for (const width of candidateWidths()) {
    const blob = await renderCanonical(source, bounds, width);
    if (blob.size <= LOGO_MAX_BYTES) {
      return new File([blob], 'logo.png', { type: 'image/png' });
    }
  }
  throw new Error('The logo is too detailed to shrink under 1 MB. Try a simpler picture.');
}
