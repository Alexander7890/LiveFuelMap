import { API_BASE_URL } from "../config";

export function cx(...values) {
  return values.flat().filter(Boolean).join(" ");
}

export function formatPrice(value) {
  const number = Number(value);
  if (!Number.isFinite(number)) return "—";
  return number.toFixed(2).replace(".", ",");
}

export function formatDateTime(value) {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";
  return date.toLocaleString("uk-UA");
}

export function imageUrl(value, fallback = "/default-station11.jpg") {
  const normalized = String(value || "").trim();
  if (!normalized || normalized === "default-station.jpg") return fallback;
  if (normalized.startsWith("http")) return normalized;
  if (normalized.startsWith("/")) return `${API_BASE_URL}${normalized}`;
  return `/${normalized}`;
}

export function truncate(value, length = 120) {
  const text = String(value || "").replace(/\s+/g, " ").trim();
  return text.length > length ? `${text.slice(0, length - 1)}…` : text;
}

export function normalizeRating(value) {
  const rating = Number(value);
  if (!Number.isFinite(rating)) return 0;
  return Math.min(5, Math.max(0, Math.round(rating)));
}

export function createIdempotencyKey() {
  if (crypto?.randomUUID) return crypto.randomUUID();
  return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

export function downloadBlob(blob, fileName) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.append(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

export function filenameFromDisposition(disposition, fallback) {
  if (!disposition) return fallback;
  const encoded = disposition.match(/filename\*=UTF-8''([^;]+)/i);
  if (encoded?.[1]) return decodeURIComponent(encoded[1]);
  const plain = disposition.match(/filename="?([^"]+)"?/i);
  return plain?.[1] || fallback;
}
