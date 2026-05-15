import { API_BASE_URL } from "../config";
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

async function readResponse(response) {
  if (response.status === 204) return null;
  const contentType = response.headers.get("content-type") || "";
  if (contentType.includes("application/json")) return response.json();
  return response.text();
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
        if (!response.ok) throw new Error(payload?.error || payload?.message || "Session expired.");
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

  const response = await fetch(apiUrl(path), { ...options, headers });
  const payload = await readResponse(response);

  if (response.status === 401 && allowRefresh && getRefreshToken()) {
    await refreshAccessToken();
    return fetchJson(path, options, false);
  }

  if (!response.ok) {
    throw new Error(payload?.error || payload?.message || payload || `HTTP ${response.status}`);
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
    throw new Error(payload?.error || payload?.message || payload || "Не вдалося сформувати файл.");
  }

  const blob = await response.blob();
  const extension = format === "excel" ? "xlsx" : format;
  const fileName = filenameFromDisposition(response.headers.get("content-disposition"), `livefuelmap-report.${extension}`);
  return { blob, fileName };
}

export const api = {
  auth: {
    login: body => fetchJson("/api/auth/login", { method: "POST", body: JSON.stringify(body) }),
    register: body => fetchJson("/api/auth/register", { method: "POST", body: JSON.stringify(body) }),
    verify: () => fetchJson("/api/auth/verify"),
    logout: refreshToken => fetchJson("/api/auth/logout", { method: "POST", body: JSON.stringify({ refreshToken }) }),
    verifyEmail: code => fetchJson("/api/auth/verify-email", { method: "POST", body: JSON.stringify({ code }) }),
    resendVerification: () => fetchJson("/api/auth/resend-verification-code", { method: "POST" }),
    nicknameSuggestions: nickname => fetchJson(`/api/auth/nickname-suggestions?nickname=${encodeURIComponent(nickname || "")}`)
  },
  profile: {
    get: () => fetchJson("/api/profile"),
    update: body => fetchJson("/api/profile", { method: "PUT", body: JSON.stringify(body) }),
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
    delete: id => fetchJson(`/api/subscriptions/${id}`, { method: "DELETE" })
  },
  chat: {
    send: body => fetchJson("/api/chat", { method: "POST", body: JSON.stringify(body) }),
    history: sessionId => fetchJson(`/api/chat/history?sessionId=${encodeURIComponent(sessionId)}`),
    clear: sessionId => fetchJson(`/api/chat/history?sessionId=${encodeURIComponent(sessionId)}`, { method: "DELETE" })
  },
  report: request => fetchFile("/api/reports/fuel-prices/export", request, request.format),
  admin: {
    users: () => fetchJson("/api/admin/users"),
    updateUserRole: (id, role) => fetchJson(`/api/admin/users/${id}/role`, { method: "PUT", body: JSON.stringify({ role }) }),
    deleteUser: id => fetchJson(`/api/admin/users/${id}`, { method: "DELETE" }),
    comments: () => fetchJson("/api/admin/comments"),
    updateComment: (id, body) => fetchJson(`/api/admin/comments/${id}`, { method: "PUT", body: JSON.stringify(body) }),
    deleteComment: id => fetchJson(`/api/admin/comments/${id}`, { method: "DELETE" }),
    apiTokens: () => fetchJson("/api/admin/api-tokens"),
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
