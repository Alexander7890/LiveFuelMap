import { API_BASE_URL } from "../config";
import { translateApiErrors, translateApiMessage } from "../i18n/apiMessages";
import { createIdempotencyKey, filenameFromDisposition } from "../utils/format";

export const tokenStorageKey = "token";
export const refreshTokenStorageKey = "refreshToken";

let refreshPromise = null;

export function getAccessToken() {
  return localStorage.getItem(tokenStorageKey);
}

export function getRefreshToken() {
  return localStorage.getItem(refreshTokenStorageKey);
}

export function persistAuth(data) {
  if (!data) return;
  localStorage.setItem(tokenStorageKey, data.token);
  localStorage.setItem(refreshTokenStorageKey, data.refreshToken);
}

export function clearAuthStorage() {
  localStorage.removeItem(tokenStorageKey);
  localStorage.removeItem(refreshTokenStorageKey);
}

export function authHeaders() {
  const token = getAccessToken();
  return token ? { Authorization: `Bearer ${token}` } : {};
}

export function apiUrl(path) {
  if (path.startsWith("http")) return path;
  return `${API_BASE_URL}${path}`;
}

export class ApiError extends Error {
  constructor(message, { status = 0, errors = {}, suggestions = [], payload = null } = {}) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.errors = errors || {};
    this.suggestions = suggestions || [];
    this.payload = payload;
  }
}

async function readResponse(response) {
  if (response.status === 204) return null;
  const contentType = response.headers.get("content-type") || "";
  if (contentType.includes("application/json")) return response.json();
  return response.text();
}

function normalizeApiMessage(payload, fallback) {
  if (!payload) return fallback;
  if (typeof payload === "string") return payload || fallback;
  return payload.message || payload.error || fallback;
}

function normalizeApiErrors(payload) {
  if (!payload || typeof payload !== "object" || !payload.errors) return {};
  return Object.fromEntries(
    Object.entries(payload.errors).map(([key, value]) => [
      key,
      Array.isArray(value) ? value.map(String) : [String(value)]
    ])
  );
}

function createApiError(response, payload, fallback) {
  const errors = normalizeApiErrors(payload);
  return new ApiError(translateApiMessage(normalizeApiMessage(payload, fallback), fallback), {
    status: response.status,
    errors: translateApiErrors(errors),
    suggestions: payload?.suggestions || [],
    payload
  });
}

export async function refreshAccessToken() {
  const refreshToken = getRefreshToken();
  if (!refreshToken) throw new Error("Refresh token is missing.");

  if (!refreshPromise) {
    refreshPromise = fetch(apiUrl("/api/auth/refresh"), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken })
    })
      .then(async response => {
        const payload = await readResponse(response);
        if (!response.ok) throw createApiError(response, payload, "sessionExpired");
        persistAuth(payload);
        return payload;
      })
      .finally(() => {
        refreshPromise = null;
      });
  }

  return refreshPromise;
}

export async function fetchJson(path, options = {}, allowRefresh = true) {
  const headers = {
    ...(options.body instanceof FormData ? {} : { "Content-Type": "application/json" }),
    ...authHeaders(),
    ...(options.headers || {})
  };

  let response;
  try {
    response = await fetch(apiUrl(path), { ...options, headers });
  } catch (error) {
    throw new ApiError(translateApiMessage("networkError"), {
      status: 0,
      payload: error
    });
  }

  const payload = await readResponse(response);

  if (response.status === 401 && allowRefresh && getRefreshToken()) {
    await refreshAccessToken();
    return fetchJson(path, options, false);
  }

  if (!response.ok) {
    throw createApiError(response, payload, "genericActionFailed");
  }

  return payload;
}

export async function fetchFile(path, request, format) {
  const response = await fetch(apiUrl(path), {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "Idempotency-Key": createIdempotencyKey(),
      ...authHeaders()
    },
    body: JSON.stringify(request)
  });

  if (response.status === 401 && getRefreshToken()) {
    await refreshAccessToken();
    return fetchFile(path, request, format);
  }

  if (!response.ok) {
    const payload = await readResponse(response);
    throw createApiError(response, payload, "fileBuildFailed");
  }

  const blob = await response.blob();
  const extension = format === "excel" ? "xlsx" : format;
  const fileName = filenameFromDisposition(response.headers.get("content-disposition"), `livefuelmap-report.${extension}`);
  return { blob, fileName };
}

function queryString(query) {
  const params = new URLSearchParams();
  Object.entries(query || {}).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") params.set(key, value);
  });
  const suffix = params.toString();
  return suffix ? `?${suffix}` : "";
}

export const api = {
  auth: {
    login: body => fetchJson("/api/auth/login", { method: "POST", body: JSON.stringify(body) }),
    googleStartUrl: returnUrl => apiUrl(`/api/auth/google/start${returnUrl ? `?returnUrl=${encodeURIComponent(returnUrl)}` : ""}`),
    googleCredential: credential => fetchJson("/api/auth/google/credential", { method: "POST", body: JSON.stringify({ credential }) }),
    register: body => fetchJson("/api/auth/register", { method: "POST", body: JSON.stringify(body) }),
    verify: () => fetchJson("/api/auth/verify"),
    logout: refreshToken => fetchJson("/api/auth/logout", { method: "POST", body: JSON.stringify({ refreshToken }) }),
    verifyEmail: code => fetchJson("/api/auth/verify-email", { method: "POST", body: JSON.stringify({ code }) }),
    resendVerification: () => fetchJson("/api/auth/resend-verification-code", { method: "POST" }),
    nicknameSuggestions: nickname => fetchJson(`/api/auth/nickname-suggestions?nickname=${encodeURIComponent(nickname || "")}`)
  },
  profile: {
    get: () => fetchJson("/api/profile"),
    setupNickname: body => fetchJson("/api/profile/setup-nickname", { method: "POST", body: JSON.stringify(body) }),
    update: body => fetchJson("/api/profile", { method: "PUT", body: JSON.stringify(body) }),
    requestDeletionCode: body => fetchJson("/api/profile/delete-code", { method: "POST", body: JSON.stringify(body) }),
    uploadPhoto: file => {
      const form = new FormData();
      form.append("file", file);
      return fetchJson("/api/profile/photo", { method: "POST", body: form });
    },
    delete: body => fetchJson("/api/profile", { method: "DELETE", body: JSON.stringify(body) })
  },
  fuels: () => fetchJson("/api/fuels"),
  stations: query => {
    const params = new URLSearchParams({ pageSize: "100" });
    Object.entries(query || {}).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== "") params.set(key, value);
    });
    return fetchJson(`/api/stations?${params}`);
  },
  priceHistory: query => {
    const params = new URLSearchParams();
    Object.entries(query || {}).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== "") params.set(key, value);
    });
    return fetchJson(`/api/fuel-prices/history?${params}`);
  },
  correctPrice: body => fetchJson("/api/fuel-prices/corrections", {
    method: "POST",
    headers: { "Idempotency-Key": createIdempotencyKey() },
    body: JSON.stringify(body)
  }),
  comments: {
    list: stationId => fetchJson(`/api/comments${stationId ? `?stationId=${stationId}` : ""}`),
    update: (id, body) => fetchJson(`/api/comments/${id}`, { method: "PUT", body: JSON.stringify(body) }),
    delete: id => fetchJson(`/api/comments/${id}`, { method: "DELETE" })
  },
  compare: stationIds => fetchJson("/api/compare", { method: "POST", body: JSON.stringify({ stationIds }) }),
  subscriptions: {
    list: () => fetchJson("/api/subscriptions"),
    create: body => fetchJson("/api/subscriptions", { method: "POST", body: JSON.stringify(body) }),
    update: (id, body) => fetchJson(`/api/subscriptions/${id}`, { method: "PUT", body: JSON.stringify(body) }),
    delete: id => fetchJson(`/api/subscriptions/${id}`, { method: "DELETE" })
  },
  chat: {
    send: body => fetchJson("/api/chat", { method: "POST", body: JSON.stringify(body) }),
    history: sessionId => fetchJson(`/api/chat/history?sessionId=${encodeURIComponent(sessionId)}`),
    clear: sessionId => fetchJson(`/api/chat/history?sessionId=${encodeURIComponent(sessionId)}`, { method: "DELETE" })
  },
  report: request => fetchFile("/api/reports/fuel-prices/export", request, request.format),
  admin: {
    users: query => fetchJson(`/api/admin/users${queryString(query)}`),
    updateUserRole: (id, role) => fetchJson(`/api/admin/users/${id}/role`, { method: "PUT", body: JSON.stringify({ role }) }),
    deleteUser: id => fetchJson(`/api/admin/users/${id}`, { method: "DELETE" }),
    comments: query => fetchJson(`/api/admin/comments${queryString(query)}`),
    updateComment: (id, body) => fetchJson(`/api/admin/comments/${id}`, { method: "PUT", body: JSON.stringify(body) }),
    deleteComment: id => fetchJson(`/api/admin/comments/${id}`, { method: "DELETE" }),
    apiTokens: query => fetchJson(`/api/admin/api-tokens${queryString(query)}`),
    createApiToken: body => fetchJson("/api/admin/api-tokens", { method: "POST", body: JSON.stringify(body) }),
    revokeApiToken: id => fetchJson(`/api/admin/api-tokens/${id}`, { method: "DELETE" }),
    deleteApiToken: id => fetchJson(`/api/admin/api-tokens/${id}/permanent`, { method: "DELETE" }),
    testApiToken: (path, token) => fetchJson(path, { headers: { "X-API-Token": token } }, false),
    createStation: body => fetchJson("/api/stations", { method: "POST", body: JSON.stringify(body) }),
    updateStation: (id, body) => fetchJson(`/api/stations/${id}`, { method: "PUT", body: JSON.stringify(body) }),
    deleteStation: id => fetchJson(`/api/stations/${id}`, { method: "DELETE" }),
    metrics: () => fetchJson("/api/admin/metrics"),
    dataSources: () => fetchJson("/api/admin/data-sources"),
    parserRuns: () => fetchJson("/api/admin/data-sources/parser-runs"),
    runParser: () => fetchJson("/api/admin/data-sources/parser/run", { method: "POST" })
  }
};
