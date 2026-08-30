const DEFAULT_BACKEND_PORT = '5000';
const ANGULAR_DEV_PORT = '4200';
const API_PREFIX = '/api';

export function buildApiUrl(path: string): string {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  const origin = getBackendOrigin();
  return origin
    ? `${origin}${API_PREFIX}${normalizedPath}`
    : `${API_PREFIX}${normalizedPath}`;
}

function getBackendOrigin(): string {
  if (typeof window !== 'undefined') {
    const { origin, protocol, hostname, port } = window.location;
    if (!hostname) {
      return '';
    }

    if (port === ANGULAR_DEV_PORT) {
      return `${protocol}//${hostname}:${DEFAULT_BACKEND_PORT}`;
    }

    if (!port) {
      return origin;
    }

    return `${protocol}//${hostname}:${port}`;
  }

  if (typeof process === 'undefined') {
    return '';
  }

  return normalizeOrigin(process.env['BASE_IP']);
}

function normalizeOrigin(value?: string): string {
  const trimmed = value?.trim().replace(/\/+$/, '') ?? '';
  if (!trimmed) {
    return '';
  }

  if (trimmed.startsWith('http://') || trimmed.startsWith('https://')) {
    return trimmed;
  }

  return `http://${trimmed}`;
}
