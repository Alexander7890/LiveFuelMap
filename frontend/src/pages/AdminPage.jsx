import { Ban, ChevronLeft, ChevronRight, Copy, Eye, EyeOff, KeyRound, Play, RefreshCw, RotateCcw, Save, Search, ShieldCheck, Trash2, UserCog } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { Button, Card, Input, Section, Select, Textarea } from "../components/common/Ui";
import { useData } from "../contexts/DataContext";
import { useToast } from "../contexts/ToastContext";
import { api } from "../services/api";
import { formatDateTime, formatPrice } from "../utils/format";
import { useTranslation } from "react-i18next";

const scopeOptions = [
  { value: "fuel:read", titleKey: "admin.scopes.fuelReadTitle", textKey: "admin.scopes.fuelReadText" },
  { value: "comments:read", titleKey: "admin.scopes.commentsReadTitle", textKey: "admin.scopes.commentsReadText" },
  { value: "read:all", titleKey: "admin.scopes.readAllTitle", textKey: "admin.scopes.readAllText" },
  { value: "read:all,fuel:write", titleKey: "admin.scopes.readFuelWriteTitle", textKey: "admin.scopes.readFuelWriteText" },
  { value: "fuel:write", titleKey: "admin.scopes.fuelWriteTitle", textKey: "admin.scopes.fuelWriteText" },
  { value: "write:all", titleKey: "admin.scopes.writeAllTitle", textKey: "admin.scopes.writeAllText" },
  { value: "*", titleKey: "admin.scopes.wildcardTitle", textKey: "admin.scopes.wildcardText" }
];

const tokenTestModes = [
  { value: "current-prices", labelKey: "admin.tokenTest.currentPrices", path: "/api/external/current-prices" },
  { value: "fuels", labelKey: "admin.tokenTest.fuels", path: "/api/external/fuels" },
  { value: "stations", labelKey: "admin.tokenTest.stations", path: "/api/external/stations" },
  { value: "history", labelKey: "admin.tokenTest.history", path: "/api/external/price-history?fuel=all" },
  { value: "comments", labelKey: "admin.tokenTest.comments", path: "/api/external/comments" },
  { value: "write-check", labelKey: "admin.tokenTest.writeCheck", path: "/api/external/write-check" }
];

const emptyStation = { id: "", name: "", address: "", city: "Харків", latitude: "", longitude: "", imageUrl: "", websiteUrl: "", photoUrls: "" };
const defaultUserQuery = { search: "", role: "", provider: "", status: "", createdFrom: "", createdTo: "", page: 1, pageSize: 20 };
const defaultTokenQuery = { search: "", status: "", scope: "", userEmail: "", createdFrom: "", createdTo: "", expiresFrom: "", expiresTo: "", page: 1, pageSize: 8 };
const defaultCommentQuery = { search: "", author: "", status: "", dateFrom: "", dateTo: "", page: 1, pageSize: 20 };

function maskToken(value) {
  if (!value) return "";
  if (value.length <= 18) return "••••••••";
  return `${value.slice(0, 8)}••••••••${value.slice(-6)}`;
}

function formatApiResult(value) {
  const text = typeof value === "string" ? value : JSON.stringify(value, null, 2);
  return text.length > 4800 ? `${text.slice(0, 4800)}\n...` : text;
}

function truncateTokenName(value) {
  const text = String(value || "");
  return text.length > 30 ? `${text.slice(0, 30)}...` : text;
}

function userStatusLabel(user, t) {
  return user?.status === "deleted" || user?.isDeleted ? t("admin.deleted") : t("admin.active");
}

function userStatusClass(user) {
  return user?.status === "deleted" || user?.isDeleted
    ? "bg-rose-50 text-rose-700 dark:bg-rose-500/10 dark:text-rose-200"
    : "bg-emerald-50 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-200";
}

function tokenStatusLabel(status, t) {
  return ({ active: t("admin.active"), revoked: t("admin.revoked"), expired: t("admin.expired") })[status] || t("admin.unknown");
}

function tokenStatusClass(status) {
  return ({
    active: "bg-emerald-50 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-200",
    revoked: "bg-rose-50 text-rose-700 dark:bg-rose-500/10 dark:text-rose-200",
    expired: "bg-amber-50 text-amber-700 dark:bg-amber-500/10 dark:text-amber-200"
  })[status] || "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200";
}

function commentAuthorLabel(type, t) {
  return ({ registered: t("admin.registered"), deleted: t("admin.deletedUser"), guest: t("admin.guest") })[type] || t("admin.author");
}

export default function AdminPage() {
  const { t } = useTranslation();
  const { stations, fuels, reloadFuelData } = useData();
  const { showToast } = useToast();
  const [tab, setTab] = useState("prices");
  const [users, setUsers] = useState([]);
  const [usersLoading, setUsersLoading] = useState(false);
  const [userQuery, setUserQuery] = useState(defaultUserQuery);
  const [userPageInfo, setUserPageInfo] = useState({ page: 1, pageSize: 20, totalCount: 0 });
  const [tokens, setTokens] = useState([]);
  const [tokensLoading, setTokensLoading] = useState(false);
  const [tokenQuery, setTokenQuery] = useState(defaultTokenQuery);
  const [tokenPageInfo, setTokenPageInfo] = useState({ page: 1, pageSize: 20, totalCount: 0 });
  const [comments, setComments] = useState([]);
  const [commentsLoading, setCommentsLoading] = useState(false);
  const [commentQuery, setCommentQuery] = useState(defaultCommentQuery);
  const [commentPageInfo, setCommentPageInfo] = useState({ page: 1, pageSize: 20, totalCount: 0 });
  const [price, setPrice] = useState({ stationId: "", fuelId: "", price: "", popularity: 0, date: new Date().toISOString().slice(0, 10) });
  const [stationForm, setStationForm] = useState(emptyStation);
  const [tokenForm, setTokenForm] = useState({ name: "", scopes: "fuel:read", expiresAt: "" });
  const [createdToken, setCreatedToken] = useState(null);
  const [showCreatedToken, setShowCreatedToken] = useState(false);
  const [tokenTest, setTokenTest] = useState({ value: "", mode: "current-prices", loading: false, result: "", status: "" });

  async function reloadUsers(query = userQuery) {
    setUsersLoading(true);
    try {
      const result = await api.admin.users(query);
      setUsers(result?.items || []);
      setUserPageInfo({
        page: result?.page || query.page || 1,
        pageSize: result?.pageSize || query.pageSize || 20,
        totalCount: result?.totalCount || 0
      });
    } catch {
      setUsers([]);
      setUserPageInfo({ page: query.page || 1, pageSize: query.pageSize || 20, totalCount: 0 });
    } finally {
      setUsersLoading(false);
    }
  }

  async function reloadTokens(query = tokenQuery) {
    setTokensLoading(true);
    try {
      const result = await api.admin.apiTokens(query);
      setTokens(result?.items || []);
      setTokenPageInfo({
        page: result?.page || query.page || 1,
        pageSize: result?.pageSize || query.pageSize || 20,
        totalCount: result?.totalCount || 0
      });
    } catch {
      setTokens([]);
      setTokenPageInfo({ page: query.page || 1, pageSize: query.pageSize || 20, totalCount: 0 });
    } finally {
      setTokensLoading(false);
    }
  }

  async function reloadComments(query = commentQuery) {
    setCommentsLoading(true);
    try {
      const result = await api.admin.comments(query);
      setComments(result?.items || []);
      setCommentPageInfo({
        page: result?.page || query.page || 1,
        pageSize: result?.pageSize || query.pageSize || 20,
        totalCount: result?.totalCount || 0
      });
    } catch {
      setComments([]);
      setCommentPageInfo({ page: query.page || 1, pageSize: query.pageSize || 20, totalCount: 0 });
    } finally {
      setCommentsLoading(false);
    }
  }

  async function reloadAdmin() {
    await Promise.all([
      reloadUsers(userQuery),
      reloadTokens(tokenQuery),
      reloadComments(commentQuery)
    ]);
  }

  useEffect(() => {
    reloadAdmin();
  }, []);

  const totalUserPages = Math.max(1, Math.ceil(userPageInfo.totalCount / userPageInfo.pageSize));
  const userRangeStart = userPageInfo.totalCount === 0 ? 0 : (userPageInfo.page - 1) * userPageInfo.pageSize + 1;
  const userRangeEnd = Math.min(userPageInfo.page * userPageInfo.pageSize, userPageInfo.totalCount);
  const totalTokenPages = Math.max(1, Math.ceil(tokenPageInfo.totalCount / tokenPageInfo.pageSize));
  const tokenRangeStart = tokenPageInfo.totalCount === 0 ? 0 : (tokenPageInfo.page - 1) * tokenPageInfo.pageSize + 1;
  const tokenRangeEnd = Math.min(tokenPageInfo.page * tokenPageInfo.pageSize, tokenPageInfo.totalCount);
  const totalCommentPages = Math.max(1, Math.ceil(commentPageInfo.totalCount / commentPageInfo.pageSize));
  const commentRangeStart = commentPageInfo.totalCount === 0 ? 0 : (commentPageInfo.page - 1) * commentPageInfo.pageSize + 1;
  const commentRangeEnd = Math.min(commentPageInfo.page * commentPageInfo.pageSize, commentPageInfo.totalCount);

  function applyUserQuery(patch, resetPage = true) {
    const next = {
      ...userQuery,
      ...patch,
      page: resetPage ? 1 : patch.page ?? userQuery.page
    };
    setUserQuery(next);
    reloadUsers(next);
  }

  function submitUserSearch(event) {
    event.preventDefault();
    applyUserQuery({ page: 1 }, false);
  }

  function clearUserFilters() {
    setUserQuery(defaultUserQuery);
    reloadUsers(defaultUserQuery);
  }

  function applyTokenQuery(patch, resetPage = true) {
    const next = {
      ...tokenQuery,
      ...patch,
      page: resetPage ? 1 : patch.page ?? tokenQuery.page
    };
    setTokenQuery(next);
    reloadTokens(next);
  }

  function submitTokenSearch(event) {
    event.preventDefault();
    applyTokenQuery({ page: 1 }, false);
  }

  function clearTokenFilters() {
    setTokenQuery(defaultTokenQuery);
    reloadTokens(defaultTokenQuery);
  }

  function applyCommentQuery(patch, resetPage = true) {
    const next = {
      ...commentQuery,
      ...patch,
      page: resetPage ? 1 : patch.page ?? commentQuery.page
    };
    setCommentQuery(next);
    reloadComments(next);
  }

  function submitCommentSearch(event) {
    event.preventDefault();
    applyCommentQuery({ page: 1 }, false);
  }

  function clearCommentFilters() {
    setCommentQuery(defaultCommentQuery);
    reloadComments(defaultCommentQuery);
  }

  async function changeUserRole(user, role) {
    await api.admin.updateUserRole(user.id, role);
    await reloadUsers();
  }

  async function deleteUser(user) {
    if (!window.confirm(t("admin.confirmDeleteUser", { email: user.email }))) return;
    await api.admin.deleteUser(user.id);
    await reloadUsers();
  }

  const currentPrice = useMemo(() => {
    const station = stations.find(item => Number(item.id) === Number(price.stationId));
    return station?.prices?.find(item => Number(item.fuelId) === Number(price.fuelId));
  }, [stations, price.stationId, price.fuelId]);

  function loadStation(stationId) {
    const station = stations.find(item => Number(item.id) === Number(stationId));
    if (!station) {
      setStationForm(emptyStation);
      return;
    }
    setStationForm({
      id: station.id,
      name: station.name,
      address: station.address,
      city: station.city,
      latitude: station.latitude,
      longitude: station.longitude,
      imageUrl: station.imageUrl,
      websiteUrl: station.websiteUrl || "",
      photoUrls: (station.photoUrls || []).join("\n")
    });
  }

  async function savePrice(event) {
    event.preventDefault();
    try {
      await api.correctPrice({
        stationId: Number(price.stationId),
        fuelId: Number(price.fuelId),
        price: Number(price.price),
        popularity: Number(price.popularity || 0),
        date: price.date
      });
      await reloadFuelData();
      showToast(t("admin.toasts.priceSavedTitle"), t("admin.toasts.priceSavedMessage"));
    } catch (error) {
      showToast(t("admin.toasts.priceSaveFailed"), error.message, "danger");
    }
  }

  async function saveStation(event) {
    event.preventDefault();
    const body = {
      name: stationForm.name,
      address: stationForm.address,
      city: stationForm.city,
      latitude: Number(stationForm.latitude),
      longitude: Number(stationForm.longitude),
      imageUrl: stationForm.imageUrl,
      websiteUrl: stationForm.websiteUrl,
      photoUrls: stationForm.photoUrls.split(/\r?\n/).map(item => item.trim()).filter(Boolean)
    };
    try {
      if (stationForm.id) await api.admin.updateStation(stationForm.id, body);
      else await api.admin.createStation(body);
      await reloadFuelData();
      showToast(t("admin.toasts.stationSavedTitle"), t("admin.toasts.stationSavedMessage"));
    } catch (error) {
      showToast(t("admin.toasts.stationSaveFailed"), error.message, "danger");
    }
  }

  async function deleteStation() {
    if (!stationForm.id || !window.confirm(t("admin.confirmDeleteStation"))) return;
    await api.admin.deleteStation(stationForm.id);
    setStationForm(emptyStation);
    await reloadFuelData();
  }

  async function createToken(event) {
    event.preventDefault();
    try {
      const created = await api.admin.createApiToken({ ...tokenForm, expiresAt: tokenForm.expiresAt || null });
      await reloadTokens();
      setCreatedToken(created);
      setShowCreatedToken(false);
      setTokenTest(current => ({ ...current, value: created.token }));
      showToast(t("admin.toasts.tokenCreatedTitle"), t("admin.toasts.tokenCreatedMessage"));
    } catch (error) {
      showToast(t("admin.toasts.tokenCreateFailed"), error.message, "danger");
    }
  }

  async function copyCreatedToken() {
    if (!createdToken?.token) return;
    await navigator.clipboard.writeText(createdToken.token);
    showToast(t("admin.toasts.tokenCopiedTitle"), t("admin.toasts.tokenCopiedMessage"));
  }

  async function testApiToken() {
    const mode = tokenTestModes.find(item => item.value === tokenTest.mode) || tokenTestModes[0];
    const value = tokenTest.value.trim();
    if (!value) {
      showToast(t("admin.toasts.tokenRequiredTitle"), t("admin.toasts.tokenRequiredMessage"), "warning");
      return;
    }

    setTokenTest(current => ({ ...current, loading: true, status: "", result: "" }));
    try {
      const result = await api.admin.testApiToken(mode.path, value);
      setTokenTest(current => ({
        ...current,
        loading: false,
        status: t("admin.accessAllowed"),
        result: formatApiResult(result)
      }));
    } catch (error) {
      setTokenTest(current => ({
        ...current,
        loading: false,
        status: t("admin.accessDenied"),
        result: error.message
      }));
    }
  }

  async function revokeToken(token) {
    if (token.revokedAt || !window.confirm(t("admin.confirmRevokeToken", { name: token.name }))) return;
    try {
      await api.admin.revokeApiToken(token.id);
      await reloadTokens();
      showToast(t("admin.toasts.tokenRevokedTitle"), t("admin.toasts.tokenRevokedMessage"));
    } catch (error) {
      showToast(t("admin.toasts.tokenRevokeFailed"), error.message, "danger");
    }
  }

  async function deleteToken(token) {
    if (!window.confirm(t("admin.confirmDeleteToken", { name: token.name }))) return;
    try {
      await api.admin.deleteApiToken(token.id);
      await reloadTokens();
      showToast(t("admin.toasts.tokenDeletedTitle"), t("admin.toasts.tokenDeletedMessage"));
    } catch (error) {
      showToast(t("admin.toasts.tokenDeleteFailed"), error.message, "danger");
    }
  }

  async function runParser() {
    try {
      await api.admin.runParser();
      await reloadFuelData();
      showToast(t("admin.toasts.parserStartedTitle"), t("admin.toasts.parserStartedMessage"));
    } catch (error) {
      showToast(t("admin.toasts.parserFailed"), error.message, "danger");
    }
  }

  return (
    <Section title={t("admin.title")} subtitle={t("admin.subtitle")} className="max-w-[96rem]">
      <div className="mb-5 flex flex-wrap gap-2 overflow-x-auto pb-1">
        {[
          ["prices", t("admin.prices")],
          ["stations", t("admin.stations")],
          ["tokens", t("admin.apiTokens")],
          ["users", t("admin.users")],
          ["comments", t("admin.comments")]
        ].map(([id, label]) => (
          <button key={id} onClick={() => setTab(id)} className={`rounded-full px-4 py-2 text-sm font-bold transition ${tab === id ? "bg-brand-600 text-white" : "bg-slate-100 text-slate-700 dark:bg-slate-900 dark:text-slate-200"}`}>{label}</button>
        ))}
        <Button variant="secondary" onClick={runParser}><Play className="h-4 w-4" /> {t("admin.runParser")}</Button>
      </div>

      {tab === "prices" && (
        <Card as="form" onSubmit={savePrice} className="grid gap-4">
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
            <Select label={t("common.station")} value={price.stationId} onChange={event => setPrice({ ...price, stationId: event.target.value })} required>
              <option value="">{t("admin.selectStation")}</option>
              {stations.map(station => <option key={station.id} value={station.id}>{station.name} · {station.address}</option>)}
            </Select>
            <Select label={t("common.fuel")} value={price.fuelId} onChange={event => setPrice({ ...price, fuelId: event.target.value })} required>
              <option value="">{t("admin.selectFuel")}</option>
              {fuels.map(fuel => <option key={fuel.id} value={fuel.id}>{fuel.name}</option>)}
            </Select>
            <Input type="number" step="0.01" label={t("admin.newPrice")} value={price.price} onChange={event => setPrice({ ...price, price: event.target.value })} required />
            <Input type="number" label={t("admin.popularity")} value={price.popularity} onChange={event => setPrice({ ...price, popularity: event.target.value })} />
            <Input type="date" label={t("common.date")} value={price.date} onChange={event => setPrice({ ...price, date: event.target.value })} required />
          </div>
          <div className="soft-panel">{t("admin.currentPrice")}: <strong>{currentPrice ? `${formatPrice(currentPrice.price)} ${t("common.currencyShort")}` : t("common.none")}</strong></div>
          <Button className="w-full sm:w-fit"><Save className="h-4 w-4" /> {t("admin.savePrice")}</Button>
        </Card>
      )}

      {tab === "stations" && (
        <Card as="form" onSubmit={saveStation} className="grid gap-4">
          <Select label={t("admin.loadStation")} value={stationForm.id} onChange={event => loadStation(event.target.value)}>
            <option value="">{t("admin.newStation")}</option>
            {stations.map(station => <option key={station.id} value={station.id}>{station.name} · {station.address}</option>)}
          </Select>
          <div className="grid gap-4 md:grid-cols-2">
            <Input label={t("admin.name")} value={stationForm.name} onChange={event => setStationForm({ ...stationForm, name: event.target.value })} required />
            <Input label={t("common.city")} value={stationForm.city} onChange={event => setStationForm({ ...stationForm, city: event.target.value })} required />
            <Input label={t("admin.address")} value={stationForm.address} onChange={event => setStationForm({ ...stationForm, address: event.target.value })} required />
            <Input label={t("admin.mainPhoto")} value={stationForm.imageUrl} onChange={event => setStationForm({ ...stationForm, imageUrl: event.target.value })} />
            <Input label={t("admin.stationWebsite")} value={stationForm.websiteUrl} onChange={event => setStationForm({ ...stationForm, websiteUrl: event.target.value })} />
            <Input label="Latitude" value={stationForm.latitude} onChange={event => setStationForm({ ...stationForm, latitude: event.target.value })} required />
            <Input label="Longitude" value={stationForm.longitude} onChange={event => setStationForm({ ...stationForm, longitude: event.target.value })} required />
          </div>
          <Textarea label={t("admin.backPhotos")} value={stationForm.photoUrls} onChange={event => setStationForm({ ...stationForm, photoUrls: event.target.value })} />
          <div className="flex flex-wrap gap-2">
            <Button><Save className="h-4 w-4" /> {t("admin.saveStation")}</Button>
            {stationForm.id && <Button type="button" variant="danger" onClick={deleteStation}><Trash2 className="h-4 w-4" /> {t("common.delete")}</Button>}
          </div>
        </Card>
      )}

      {tab === "tokens" && (
        <div className="grid gap-5 xl:grid-cols-[minmax(320px,380px)_minmax(0,1fr)]">
          <div className="grid min-w-0 content-start gap-5">
            <Card as="form" onSubmit={createToken} className="grid gap-4">
              <div>
                <h3 className="text-lg font-black">{t("admin.createToken")}</h3>
                <p className="mt-1 text-sm text-slate-500 dark:text-slate-300">{t("admin.rawTokenHint")}</p>
              </div>
              <Input label={t("admin.tokenName")} value={tokenForm.name} onChange={event => setTokenForm({ ...tokenForm, name: event.target.value })} maxLength={100} required />
              <div>
                <div className="mb-2 text-sm font-semibold text-slate-700 dark:text-slate-200">{t("admin.oneAccessLevel")}</div>
                <div className="grid gap-2">
                  {scopeOptions.map(scope => {
                    const selected = tokenForm.scopes === scope.value;
                    const scopeTitle = t(scope.titleKey);
                    const scopeText = t(scope.textKey);
                    return (
                      <button
                        key={scope.value}
                        type="button"
                        title={`${scopeTitle}: ${scopeText}`}
                        onClick={() => setTokenForm(current => ({ ...current, scopes: scope.value }))}
                        className={`rounded-xl border p-3 text-left text-sm transition hover:-translate-y-0.5 ${
                          selected
                            ? "border-brand-300 bg-brand-50 text-brand-900 shadow-sm dark:border-brand-500/40 dark:bg-brand-500/10 dark:text-brand-50"
                            : "border-slate-200 bg-slate-50 text-slate-700 dark:border-slate-800 dark:bg-slate-900 dark:text-slate-200"
                        }`}
                      >
                        <span className="flex items-start justify-between gap-3">
                          <span>
                            <strong className="block">{scope.value} — {scopeTitle}</strong>
                            <span className="mt-1 block text-xs text-slate-500 dark:text-slate-300">{scopeText}</span>
                          </span>
                          {selected && <span className="badge-soft shrink-0">{t("common.select")}</span>}
                        </span>
                      </button>
                    );
                  })}
                </div>
                <p className="mt-2 text-xs text-slate-500 dark:text-slate-300">{t("admin.scopeHint")}</p>
              </div>
              <Input type="datetime-local" label={t("admin.expiresAt")} value={tokenForm.expiresAt} onChange={event => setTokenForm({ ...tokenForm, expiresAt: event.target.value })} />
              <Button><KeyRound className="h-4 w-4" /> {t("admin.createTokenButton")}</Button>

              {createdToken && (
                <div className="rounded-2xl border border-emerald-200 bg-emerald-50/80 p-4 text-sm text-emerald-900 dark:border-emerald-500/30 dark:bg-emerald-500/10 dark:text-emerald-100">
                  <div className="mb-2 font-bold">{t("admin.copyTokenNow")}</div>
                  <div className="break-all rounded-xl bg-white/80 p-3 font-mono text-xs text-slate-900 dark:bg-slate-950/80 dark:text-slate-100">
                    {showCreatedToken ? createdToken.token : maskToken(createdToken.token)}
                  </div>
                  <div className="mt-3 flex flex-wrap gap-2">
                    <Button type="button" variant="secondary" onClick={() => setShowCreatedToken(value => !value)}>
                      {showCreatedToken ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                      {showCreatedToken ? t("admin.hideToken") : t("admin.showToken")}
                    </Button>
                    <Button type="button" variant="secondary" onClick={copyCreatedToken}><Copy className="h-4 w-4" /> {t("common.copy")}</Button>
                    <Button type="button" variant="ghost" onClick={() => setTokenTest(current => ({ ...current, value: createdToken.token }))}>{t("admin.pasteForTest")}</Button>
                  </div>
                </div>
              )}
            </Card>

            <Card className="grid min-w-0 max-w-full gap-4 overflow-hidden">
              <div className="min-w-0">
                <h3 className="text-lg font-black">{t("admin.testToken")}</h3>
                <p className="mt-1 text-sm text-slate-500 dark:text-slate-300">
                  {t("admin.testTokenHintPrefix")}{" "}
                  <code className="rounded bg-slate-100 px-1 py-0.5 dark:bg-slate-950">X-API-Token</code>.
                  {" "}{t("admin.testTokenHintSuffix")}
                </p>
              </div>

              <Input label="API token" value={tokenTest.value} onChange={event => setTokenTest(current => ({ ...current, value: event.target.value }))} placeholder="lfm_..." />

              <Select label={t("admin.accessTest")} value={tokenTest.mode} onChange={event => setTokenTest(current => ({ ...current, mode: event.target.value }))}>
                {tokenTestModes.map(mode => <option key={mode.value} value={mode.value}>{t(mode.labelKey)}</option>)}
              </Select>

              <Button type="button" variant="secondary" loading={tokenTest.loading} onClick={testApiToken}>
                <ShieldCheck className="h-4 w-4" /> {t("admin.checkAccess")}
              </Button>

              {(tokenTest.status || tokenTest.result) && (
                <div className="min-w-0 max-w-full overflow-hidden rounded-2xl border border-slate-200 bg-slate-50/80 p-3 dark:border-slate-800 dark:bg-slate-900/70">
                  {tokenTest.status && <div className="mb-2 text-sm font-bold">{tokenTest.status}</div>}
                  {tokenTest.result && (
                    <div className="max-h-72 max-w-full overflow-x-auto overflow-y-auto rounded-xl bg-white/80 p-3 dark:bg-slate-950/80">
                      <pre className="w-max max-w-none whitespace-pre font-mono text-xs leading-5">{tokenTest.result}</pre>
                    </div>
                  )}
                </div>
              )}
            </Card>
          </div>

          <Card className="grid content-start gap-4">
            <div>
              <h3 className="text-lg font-black">{t("admin.tokensListTitle")}</h3>
              <p className="mt-1 text-sm text-slate-500 dark:text-slate-300">{t("admin.tokensListHint")}</p>
            </div>

            <form onSubmit={submitTokenSearch} className="grid gap-3">
              <div className="grid gap-3 lg:grid-cols-[minmax(220px,1.3fr)_repeat(3,minmax(140px,1fr))]">
                <Input
                  label={t("common.search")}
                  value={tokenQuery.search}
                  onChange={event => setTokenQuery(current => ({ ...current, search: event.target.value }))}
                  placeholder={t("admin.tokenSearchPlaceholder")}
                />
                <Select label={t("common.status")} value={tokenQuery.status} onChange={event => applyTokenQuery({ status: event.target.value })}>
                  <option value="">{t("admin.allStatuses")}</option>
                  <option value="active">Active</option>
                  <option value="revoked">Revoked</option>
                  <option value="expired">Expired</option>
                </Select>
                <Input label="Scope" value={tokenQuery.scope} onChange={event => setTokenQuery(current => ({ ...current, scope: event.target.value }))} placeholder="fuel:read" />
                <Input label={t("admin.userEmail")} value={tokenQuery.userEmail} onChange={event => setTokenQuery(current => ({ ...current, userEmail: event.target.value }))} />
              </div>
              <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
                <Input type="date" label={t("admin.createdFrom")} value={tokenQuery.createdFrom} onChange={event => applyTokenQuery({ createdFrom: event.target.value })} />
                <Input type="date" label={t("admin.createdTo")} value={tokenQuery.createdTo} onChange={event => applyTokenQuery({ createdTo: event.target.value })} />
                <Input type="date" label={t("admin.expiresFrom")} value={tokenQuery.expiresFrom} onChange={event => applyTokenQuery({ expiresFrom: event.target.value })} />
                <Input type="date" label={t("admin.expiresTo")} value={tokenQuery.expiresTo} onChange={event => applyTokenQuery({ expiresTo: event.target.value })} />
              </div>
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <div className="text-sm text-slate-500 dark:text-slate-300">
                  {t("common.found")}: <strong className="text-slate-800 dark:text-slate-100">{tokenPageInfo.totalCount}</strong>
                  {tokenPageInfo.totalCount > 0 && <> · {t("common.shown")} {tokenRangeStart}-{tokenRangeEnd}</>}
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button type="submit" variant="secondary" loading={tokensLoading}><Search className="h-4 w-4" /> {t("common.find")}</Button>
                  <Button type="button" variant="ghost" onClick={clearTokenFilters}><RotateCcw className="h-4 w-4" /> {t("common.resetFilters")}</Button>
                  <Button type="button" variant="secondary" onClick={() => reloadTokens()}><RefreshCw className="h-4 w-4" /> {t("common.refresh")}</Button>
                </div>
              </div>
            </form>

            <div className="table-wrap max-h-[44rem] overflow-y-auto overscroll-contain" data-lenis-prevent-wheel>
              <table className="data-table w-full min-w-[820px] table-fixed">
                <colgroup>
                  <col className="w-[21%]" />
                  <col className="w-[13%]" />
                  <col className="w-[16%]" />
                  <col className="w-[17%]" />
                  <col className="w-[17%]" />
                  <col className="w-[16%]" />
                </colgroup>
                <thead>
                  <tr>
                    <th>{t("admin.name")}</th>
                    <th>Scope</th>
                    <th>{t("common.status")}</th>
                    <th>{t("common.createdAt")}</th>
                    <th>{t("admin.expiresAt")}</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {tokens.length === 0 && (
                    <tr>
                      <td colSpan={6} className="text-center text-sm text-slate-500 dark:text-slate-300">
                        {tokensLoading ? t("admin.loadingTokens") : t("admin.noTokens")}
                      </td>
                    </tr>
                  )}
                  {tokens.map(token => {
                    const fallbackStatus = token.revokedAt ? "revoked" : token.expiresAt && new Date(token.expiresAt) <= new Date() ? "expired" : "active";
                    const status = token.status || fallbackStatus;
                    const revoked = status === "revoked";
                    return (
                      <tr key={token.id}>
                        <td>
                          <strong title={token.name} className="block truncate">{truncateTokenName(token.name)}</strong>
                          <div title={token.createdByEmail} className="truncate text-xs text-slate-500 dark:text-slate-300">{token.createdByEmail}</div>
                        </td>
                        <td><code title={token.scopes} className="block truncate rounded bg-slate-100 px-2 py-1 text-xs dark:bg-slate-950">{token.scopes}</code></td>
                        <td>
                          <span className={`inline-flex max-w-full rounded-full px-2 py-1 text-xs font-bold ${tokenStatusClass(status)}`}>
                            {tokenStatusLabel(status, t)}
                          </span>
                          {revoked && <div className="mt-1 truncate text-xs text-slate-500 dark:text-slate-300">{formatDateTime(token.revokedAt)}</div>}
                        </td>
                        <td className="text-xs leading-5">{formatDateTime(token.createdAt)}</td>
                        <td className="text-xs leading-5">{token.expiresAt ? formatDateTime(token.expiresAt) : t("common.unlimited")}</td>
                        <td>
                          <div className="grid gap-2">
                            <Button type="button" variant="secondary" className="min-h-9 w-full px-2 py-1.5 text-xs" disabled={revoked} onClick={() => revokeToken(token)}><Ban className="h-3.5 w-3.5" /> {t("admin.revoke")}</Button>
                            <Button type="button" variant="danger" className="min-h-9 w-full px-2 py-1.5 text-xs" onClick={() => deleteToken(token)}><Trash2 className="h-3.5 w-3.5" /> {t("common.delete")}</Button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            <div className="flex flex-col gap-3 border-t border-slate-200 pt-4 dark:border-slate-800 sm:flex-row sm:items-center sm:justify-between">
              <Select
                label={t("common.perPage")}
                value={tokenQuery.pageSize}
                onChange={event => applyTokenQuery({ pageSize: Number(event.target.value) })}
                className="w-full sm:w-40"
              >
                <option value={8}>8</option>
                <option value={10}>10</option>
                <option value={20}>20</option>
                <option value={50}>50</option>
                <option value={100}>100</option>
              </Select>
              <div className="flex items-center justify-between gap-3 sm:justify-end">
                <Button
                  type="button"
                  variant="secondary"
                  disabled={tokenPageInfo.page <= 1 || tokensLoading}
                  onClick={() => applyTokenQuery({ page: tokenPageInfo.page - 1 }, false)}
                >
                  <ChevronLeft className="h-4 w-4" /> {t("common.previous")}
                </Button>
                <span className="min-w-24 text-center text-sm font-semibold text-slate-700 dark:text-slate-200">
                  {tokenPageInfo.page} / {totalTokenPages}
                </span>
                <Button
                  type="button"
                  variant="secondary"
                  disabled={tokenPageInfo.page >= totalTokenPages || tokensLoading}
                  onClick={() => applyTokenQuery({ page: tokenPageInfo.page + 1 }, false)}
                >
                  {t("common.next")} <ChevronRight className="h-4 w-4" />
                </Button>
              </div>
            </div>
          </Card>
        </div>
      )}

      {tab === "users" && (
        <Card className="grid gap-4">
          <form onSubmit={submitUserSearch} className="grid gap-3">
            <div className="grid gap-3 lg:grid-cols-[minmax(260px,1.4fr)_repeat(5,minmax(150px,1fr))]">
              <Input
                label={t("common.search")}
                value={userQuery.search}
                onChange={event => setUserQuery(current => ({ ...current, search: event.target.value }))}
                placeholder={t("admin.userSearchPlaceholder")}
              />
              <Select label={t("common.role")} value={userQuery.role} onChange={event => applyUserQuery({ role: event.target.value })}>
                <option value="">{t("admin.allRoles")}</option>
                <option value="User">User</option>
                <option value="Admin">Admin</option>
              </Select>
              <Select label={t("common.status")} value={userQuery.status} onChange={event => applyUserQuery({ status: event.target.value })}>
                <option value="">{t("admin.allStatuses")}</option>
                <option value="active">Active</option>
                <option value="deleted">Deleted</option>
              </Select>
              <Select label={t("admin.authProvider")} value={userQuery.provider} onChange={event => applyUserQuery({ provider: event.target.value })}>
                <option value="">{t("admin.allProviders")}</option>
                <option value="Local">Local</option>
                <option value="Google">Google</option>
              </Select>
              <Input type="date" label={t("admin.createdFrom")} value={userQuery.createdFrom} onChange={event => applyUserQuery({ createdFrom: event.target.value })} />
              <Input type="date" label={t("admin.createdTo")} value={userQuery.createdTo} onChange={event => applyUserQuery({ createdTo: event.target.value })} />
            </div>
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div className="text-sm text-slate-500 dark:text-slate-300">
                {t("common.found")}: <strong className="text-slate-800 dark:text-slate-100">{userPageInfo.totalCount}</strong>
                {userPageInfo.totalCount > 0 && <> · {t("common.shown")} {userRangeStart}-{userRangeEnd}</>}
              </div>
              <div className="flex flex-wrap gap-2">
                <Button type="submit" variant="secondary" loading={usersLoading}><Search className="h-4 w-4" /> {t("common.find")}</Button>
                <Button type="button" variant="ghost" onClick={clearUserFilters}><RotateCcw className="h-4 w-4" /> {t("common.resetFilters")}</Button>
                <Button type="button" variant="secondary" onClick={() => reloadUsers()}><RefreshCw className="h-4 w-4" /> {t("common.refresh")}</Button>
              </div>
            </div>
          </form>

          <div className="table-wrap w-full">
            <table className="data-table w-full min-w-[980px]">
              <thead>
                <tr>
                  <th>Email</th>
                  <th>{t("common.nickname")}</th>
                  <th>{t("common.role")}</th>
                  <th>Provider</th>
                  <th>{t("common.status")}</th>
                  <th>{t("common.createdAt")}</th>
                  <th>{t("admin.confirmed")}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {users.length === 0 && (
                  <tr>
                    <td colSpan={8} className="text-center text-sm text-slate-500 dark:text-slate-300">
                      {usersLoading ? t("admin.loadingUsers") : t("admin.noUsers")}
                    </td>
                  </tr>
                )}
                {users.map(user => (
                  <tr key={user.id}>
                    <td>
                      <strong className="block break-all">{user.email}</strong>
                      <span className="text-xs text-slate-500 dark:text-slate-300">{user.displayName}</span>
                    </td>
                    <td>{user.nickname}</td>
                    <td>
                      <Select value={user.role} onChange={event => changeUserRole(user, event.target.value)}>
                        <option>User</option>
                        <option>Admin</option>
                      </Select>
                    </td>
                    <td><span className="badge-soft">{user.authProvider || "Local"}</span></td>
                    <td>
                      <span className={`inline-flex rounded-full px-2 py-1 text-xs font-bold ${userStatusClass(user)}`}>
                        {userStatusLabel(user, t)}
                      </span>
                      {user.deletedAt && <div className="mt-1 text-xs text-slate-500 dark:text-slate-300">{formatDateTime(user.deletedAt)}</div>}
                    </td>
                    <td className="text-xs leading-5">{formatDateTime(user.createdAt)}</td>
                    <td>{user.emailConfirmed ? t("common.yes") : t("common.no")}</td>
                    <td><Button variant="danger" onClick={() => deleteUser(user)}><UserCog className="h-4 w-4" /> {t("common.delete")}</Button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="flex flex-col gap-3 border-t border-slate-200 pt-4 dark:border-slate-800 sm:flex-row sm:items-center sm:justify-between">
            <Select
              label={t("common.perPage")}
              value={userQuery.pageSize}
              onChange={event => applyUserQuery({ pageSize: Number(event.target.value) })}
              className="w-full sm:w-40"
            >
              <option value={10}>10</option>
              <option value={20}>20</option>
              <option value={50}>50</option>
              <option value={100}>100</option>
            </Select>
            <div className="flex items-center justify-between gap-3 sm:justify-end">
              <Button
                type="button"
                variant="secondary"
                disabled={userPageInfo.page <= 1 || usersLoading}
                onClick={() => applyUserQuery({ page: userPageInfo.page - 1 }, false)}
              >
                <ChevronLeft className="h-4 w-4" /> {t("common.previous")}
              </Button>
              <span className="min-w-24 text-center text-sm font-semibold text-slate-700 dark:text-slate-200">
                {userPageInfo.page} / {totalUserPages}
              </span>
              <Button
                type="button"
                variant="secondary"
                disabled={userPageInfo.page >= totalUserPages || usersLoading}
                onClick={() => applyUserQuery({ page: userPageInfo.page + 1 }, false)}
              >
                {t("common.next")} <ChevronRight className="h-4 w-4" />
              </Button>
            </div>
          </div>
        </Card>
      )}

      {tab === "comments" && (
        <Card className="grid gap-4">
          <form onSubmit={submitCommentSearch} className="grid gap-3">
            <div className="grid gap-3 lg:grid-cols-[minmax(260px,1.4fr)_repeat(4,minmax(150px,1fr))]">
              <Input
                label={t("common.search")}
                value={commentQuery.search}
                onChange={event => setCommentQuery(current => ({ ...current, search: event.target.value }))}
                placeholder={t("admin.commentSearchPlaceholder")}
              />
              <Select label={t("admin.author")} value={commentQuery.author} onChange={event => applyCommentQuery({ author: event.target.value })}>
                <option value="">{t("admin.allAuthors")}</option>
                <option value="registered">{t("admin.registered")}</option>
                <option value="deleted">{t("admin.deletedUser")}</option>
                <option value="guest">{t("admin.guest")}</option>
              </Select>
              <Select label={t("common.status")} value={commentQuery.status} onChange={event => applyCommentQuery({ status: event.target.value })}>
                <option value="">{t("admin.allStatuses")}</option>
                <option value="published">{t("admin.published")}</option>
              </Select>
              <Input type="date" label={t("admin.dateFrom")} value={commentQuery.dateFrom} onChange={event => applyCommentQuery({ dateFrom: event.target.value })} />
              <Input type="date" label={t("admin.dateTo")} value={commentQuery.dateTo} onChange={event => applyCommentQuery({ dateTo: event.target.value })} />
            </div>
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div className="text-sm text-slate-500 dark:text-slate-300">
                {t("common.found")}: <strong className="text-slate-800 dark:text-slate-100">{commentPageInfo.totalCount}</strong>
                {commentPageInfo.totalCount > 0 && <> · {t("common.shown")} {commentRangeStart}-{commentRangeEnd}</>}
              </div>
              <div className="flex flex-wrap gap-2">
                <Button type="submit" variant="secondary" loading={commentsLoading}><Search className="h-4 w-4" /> {t("common.find")}</Button>
                <Button type="button" variant="ghost" onClick={clearCommentFilters}><RotateCcw className="h-4 w-4" /> {t("common.resetFilters")}</Button>
                <Button type="button" variant="secondary" onClick={() => reloadComments()}><RefreshCw className="h-4 w-4" /> {t("common.refresh")}</Button>
              </div>
            </div>
          </form>

          <div className="max-h-[34rem] overflow-y-auto overscroll-contain pr-1" data-lenis-prevent-wheel>
            <div className="grid gap-3">
              {comments.length === 0 && (
                <div className="rounded-2xl border border-dashed border-slate-200 p-6 text-center text-sm text-slate-500 dark:border-slate-800 dark:text-slate-300">
                  {commentsLoading ? t("admin.loadingComments") : t("admin.noComments")}
                </div>
              )}
              {comments.map(comment => (
                <div key={comment.id} className="rounded-2xl border border-slate-200 p-4 dark:border-slate-800">
                  <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                    <div className="min-w-0">
                      <div className="truncate font-bold">{comment.authorName} · {comment.stationName}</div>
                      <div className="mt-1 flex flex-wrap gap-2 text-xs text-slate-500 dark:text-slate-300">
                        <span>{comment.authorEmail}</span>
                        {comment.authorNickname && <span>@{comment.authorNickname}</span>}
                        <span>{commentAuthorLabel(comment.authorType, t)}</span>
                        <span>{formatDateTime(comment.createdAt)}</span>
                      </div>
                    </div>
                    <span className="badge-soft shrink-0">{comment.status || "published"}</span>
                  </div>
                  <p className="mt-3 whitespace-pre-line text-sm leading-6">{comment.content}</p>
                  <div className="mt-3 flex flex-wrap items-center justify-between gap-2">
                    <div className="text-xs text-slate-500 dark:text-slate-300">
                      {t("common.rating")}: <strong>{comment.rating}</strong>{comment.fuelName ? <> · {comment.fuelName}</> : null}
                    </div>
                    <Button
                      type="button"
                      variant="danger"
                      onClick={() => api.admin.deleteComment(comment.id).then(() => reloadComments())}
                    >
                      <Trash2 className="h-4 w-4" /> {t("common.delete")}
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="flex flex-col gap-3 border-t border-slate-200 pt-4 dark:border-slate-800 sm:flex-row sm:items-center sm:justify-between">
            <Select
              label={t("common.perPage")}
              value={commentQuery.pageSize}
              onChange={event => applyCommentQuery({ pageSize: Number(event.target.value) })}
              className="w-full sm:w-40"
            >
              <option value={10}>10</option>
              <option value={20}>20</option>
              <option value={50}>50</option>
              <option value={100}>100</option>
            </Select>
            <div className="flex items-center justify-between gap-3 sm:justify-end">
              <Button
                type="button"
                variant="secondary"
                disabled={commentPageInfo.page <= 1 || commentsLoading}
                onClick={() => applyCommentQuery({ page: commentPageInfo.page - 1 }, false)}
              >
                <ChevronLeft className="h-4 w-4" /> {t("common.previous")}
              </Button>
              <span className="min-w-24 text-center text-sm font-semibold text-slate-700 dark:text-slate-200">
                {commentPageInfo.page} / {totalCommentPages}
              </span>
              <Button
                type="button"
                variant="secondary"
                disabled={commentPageInfo.page >= totalCommentPages || commentsLoading}
                onClick={() => applyCommentQuery({ page: commentPageInfo.page + 1 }, false)}
              >
                {t("common.next")} <ChevronRight className="h-4 w-4" />
              </Button>
            </div>
          </div>
        </Card>
      )}
    </Section>
  );
}
