/**
 * The only way the app talks to the API.
 *
 * - Same-origin `/api/*` (proxied by Next.js), so HttpOnly session cookies flow automatically and tokens never
 *   touch JavaScript.
 * - Unsafe requests carry the double-submit CSRF token from the readable `ss_csrf` cookie.
 * - A 401 triggers one shared refresh (single-flight within a tab, serialised across tabs with the Web Locks API),
 *   then the original request is retried once.
 * - Errors are RFC 9457 problem details surfaced as {@link ApiError}.
 */

export type ProblemDetails = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
};

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly fieldErrors: Record<string, string[]>;
  readonly problem: ProblemDetails;

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail || problem.title || `Request failed (${status})`);
    this.name = "ApiError";
    this.status = status;
    this.code = problem.code ?? `http.${status}`;
    this.fieldErrors = problem.errors ?? {};
    this.problem = problem;
  }

  get isValidation() {
    return this.status === 400 && Object.keys(this.fieldErrors).length > 0;
  }
}

type Query = Record<string, string | number | boolean | null | undefined | string[]>;

export type RequestOptions = {
  query?: Query;
  body?: unknown;
  signal?: AbortSignal;
  /** Required by the API for money-moving operations; reuse the same key when retrying a submission. */
  idempotencyKey?: string;
  /** Skip the automatic refresh-and-retry (used by the auth endpoints themselves). */
  noRefresh?: boolean;
};

const CSRF_COOKIE = "ss_csrf";
const SECURE_CSRF_COOKIE = "__Secure-ss_csrf";

function readCookie(name: string): string | null {
  if (typeof document === "undefined") return null;
  const match = document.cookie.split("; ").find((c) => c.startsWith(`${name}=`));
  return match ? decodeURIComponent(match.slice(name.length + 1)) : null;
}

function csrfFromCookie() {
  return readCookie(SECURE_CSRF_COOKIE) ?? readCookie(CSRF_COOKIE);
}

let csrfPromise: Promise<string> | null = null;

async function ensureCsrf(): Promise<string> {
  const existing = csrfFromCookie();
  if (existing) return existing;
  csrfPromise ??= fetch("/api/auth/csrf", { credentials: "same-origin" })
    .then((r) => r.json() as Promise<{ token: string }>)
    .then((r) => r.token)
    .finally(() => {
      csrfPromise = null;
    });
  return csrfPromise;
}

function buildUrl(path: string, query?: Query) {
  const url = new URL(`/api${path}`, typeof window === "undefined" ? "http://localhost" : window.location.origin);
  if (query) {
    for (const [key, value] of Object.entries(query)) {
      if (value === undefined || value === null || value === "") continue;
      if (Array.isArray(value)) value.forEach((v) => url.searchParams.append(key, v));
      else url.searchParams.set(key, String(value));
    }
  }
  return url.pathname + url.search;
}

async function send(method: string, path: string, options: RequestOptions): Promise<Response> {
  const headers: Record<string, string> = { Accept: "application/json" };
  let body: BodyInit | undefined;
  if (options.body instanceof FormData) {
    body = options.body;
  } else if (options.body !== undefined) {
    headers["Content-Type"] = "application/json";
    body = JSON.stringify(options.body);
  }
  if (method !== "GET" && method !== "HEAD") headers["X-CSRF-Token"] = await ensureCsrf();
  if (options.idempotencyKey) headers["Idempotency-Key"] = options.idempotencyKey;
  return fetch(buildUrl(path, options.query), { method, headers, body, credentials: "same-origin", signal: options.signal });
}

let refreshing: Promise<boolean> | null = null;

/** Rotates the session. Resolves false when the session is gone (caller should send the user to sign-in). */
export function refreshSession(): Promise<boolean> {
  refreshing ??= (async () => {
    const attempt = async () => {
      const response = await fetch("/api/auth/refresh", {
        method: "POST",
        credentials: "same-origin",
        headers: { "X-CSRF-Token": await ensureCsrf(), Accept: "application/json" },
      });
      if (response.ok) return true;
      const problem = (await response.json().catch(() => ({}))) as ProblemDetails;
      // Another tab rotated the cookie a moment ago; its new cookies are already ours.
      return problem.code === "auth.refresh_in_progress";
    };
    try {
      if (typeof navigator !== "undefined" && "locks" in navigator) {
        return await navigator.locks.request("scholar-session-refresh", attempt);
      }
      return await attempt();
    } catch {
      return false;
    }
  })().finally(() => {
    refreshing = null;
  });
  return refreshing;
}

const sessionListeners = new Set<() => void>();

/** Notified when the session cannot be recovered (the app redirects to sign-in). */
export function onSessionExpired(listener: () => void) {
  sessionListeners.add(listener);
  return () => sessionListeners.delete(listener);
}

async function parseProblem(response: Response): Promise<ProblemDetails> {
  const type = response.headers.get("content-type") ?? "";
  if (type.includes("json")) {
    try {
      return (await response.json()) as ProblemDetails;
    } catch {
      /* fall through */
    }
  }
  return { status: response.status, title: response.statusText };
}

export async function request<T>(method: string, path: string, options: RequestOptions = {}): Promise<T> {
  let response = await send(method, path, options);

  if (response.status === 401 && !options.noRefresh && !path.startsWith("/auth/login")) {
    const recovered = await refreshSession();
    if (recovered) {
      response = await send(method, path, options);
    }
    if (response.status === 401) {
      sessionListeners.forEach((l) => l());
    }
  }

  if (!response.ok) {
    throw new ApiError(response.status, await parseProblem(response));
  }

  if (response.status === 204 || response.headers.get("content-length") === "0") {
    return undefined as T;
  }
  const type = response.headers.get("content-type") ?? "";
  return (type.includes("json") ? await response.json() : await response.blob()) as T;
}

export const api = {
  get: <T>(path: string, query?: Query, signal?: AbortSignal) => request<T>("GET", path, { query, signal }),
  post: <T>(path: string, body?: unknown, options?: Omit<RequestOptions, "body">) =>
    request<T>("POST", path, { ...options, body: body ?? {} }),
  put: <T>(path: string, body?: unknown, options?: Omit<RequestOptions, "body">) =>
    request<T>("PUT", path, { ...options, body: body ?? {} }),
  patch: <T>(path: string, body?: unknown, options?: Omit<RequestOptions, "body">) =>
    request<T>("PATCH", path, { ...options, body: body ?? {} }),
  delete: <T = void>(path: string, options?: RequestOptions) => request<T>("DELETE", path, options),
};

export type Paged<T> = {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
};
