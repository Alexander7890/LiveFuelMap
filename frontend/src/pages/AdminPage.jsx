import { Ban, Copy, Eye, EyeOff, KeyRound, Play, RefreshCw, Save, ShieldCheck, Trash2, UserCog } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { Button, Card, Input, Section, Select, Textarea } from "../components/common/Ui";
import { useData } from "../contexts/DataContext";
import { useToast } from "../contexts/ToastContext";
import { api } from "../services/api";
import { formatDateTime, formatPrice } from "../utils/format";

const scopeOptions = [
  { value: "fuel:read", title: "Fuel Data Read Scope", text: "читання цін, АЗС, типів пального та історії цін." },
  { value: "comments:read", title: "Comment Data Read Scope", text: "читання публічних коментарів та рейтингів." },
  { value: "read:all", title: "Global Read Scope", text: "повний read-only доступ до відкритих інтеграційних даних." },
  { value: "read:all,fuel:write", title: "Global Read + Fuel Write Scope", text: "читання external API та запис корекцій цін для довіреної інтеграції." },
  { value: "fuel:write", title: "Fuel Price Write Scope", text: "запис корекцій цін після погодження адміністратором." },
  { value: "write:all", title: "Global Write Scope", text: "розширений запис для довірених інтеграцій." },
  { value: "*", title: "Wildcard Administrative API Scope", text: "повний адміністративний API scope." }
];

const tokenTestModes = [
  { value: "current-prices", label: "GET current-prices / fuel:read", path: "/api/external/current-prices" },
  { value: "fuels", label: "GET fuels / fuel:read", path: "/api/external/fuels" },
  { value: "stations", label: "GET stations / fuel:read", path: "/api/external/stations" },
  { value: "history", label: "GET price-history / fuel:read", path: "/api/external/price-history?fuel=all" },
  { value: "comments", label: "GET comments / comments:read або read:all", path: "/api/external/comments" },
  { value: "write-check", label: "GET write-check / fuel:write", path: "/api/external/write-check" }
];

const emptyStation = { id: "", name: "", address: "", city: "Харків", latitude: "", longitude: "", imageUrl: "", websiteUrl: "", photoUrls: "" };

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

export default function AdminPage() {
  const { stations, fuels, reloadFuelData } = useData();
  const { showToast } = useToast();
  const [tab, setTab] = useState("prices");
  const [users, setUsers] = useState([]);
  const [tokens, setTokens] = useState([]);
  const [comments, setComments] = useState([]);
  const [price, setPrice] = useState({ stationId: "", fuelId: "", price: "", popularity: 0, date: new Date().toISOString().slice(0, 10) });
  const [stationForm, setStationForm] = useState(emptyStation);
  const [tokenForm, setTokenForm] = useState({ name: "", scopes: "fuel:read", expiresAt: "" });
  const [createdToken, setCreatedToken] = useState(null);
  const [showCreatedToken, setShowCreatedToken] = useState(false);
  const [tokenTest, setTokenTest] = useState({ value: "", mode: "current-prices", loading: false, result: "", status: "" });

  async function reloadAdmin() {
    const [userItems, tokenItems, commentItems] = await Promise.all([
      api.admin.users().catch(() => []),
      api.admin.apiTokens().catch(() => []),
      api.admin.comments().catch(() => [])
    ]);
    setUsers(userItems || []);
    setTokens(tokenItems || []);
    setComments(commentItems || []);
  }

  useEffect(() => {
    reloadAdmin();
  }, []);

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
      showToast("Ціну збережено", "WebSocket повідомить користувачів про зміну.");
    } catch (error) {
      showToast("Ціну не збережено", error.message, "danger");
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
      showToast("АЗС збережено", "Дані оператора оновлено.");
    } catch (error) {
      showToast("АЗС не збережено", error.message, "danger");
    }
  }

  async function deleteStation() {
    if (!stationForm.id || !window.confirm("Видалити АЗС?")) return;
    await api.admin.deleteStation(stationForm.id);
    setStationForm(emptyStation);
    await reloadFuelData();
  }

  async function createToken(event) {
    event.preventDefault();
    try {
      const created = await api.admin.createApiToken({ ...tokenForm, expiresAt: tokenForm.expiresAt || null });
      await reloadAdmin();
      setCreatedToken(created);
      setShowCreatedToken(false);
      setTokenTest(current => ({ ...current, value: created.token }));
      showToast("API token створено", "Токен доступний у блоці нижче. Скопіюйте його зараз.");
    } catch (error) {
      showToast("Token не створено", error.message, "danger");
    }
  }

  async function copyCreatedToken() {
    if (!createdToken?.token) return;
    await navigator.clipboard.writeText(createdToken.token);
    showToast("API token скопійовано", "Його можна вставити в інтеграцію або блок перевірки.");
  }

  async function testApiToken() {
    const mode = tokenTestModes.find(item => item.value === tokenTest.mode) || tokenTestModes[0];
    const value = tokenTest.value.trim();
    if (!value) {
      showToast("Вкажіть API token", "Вставте lfm_... перед перевіркою.", "warning");
      return;
    }

    setTokenTest(current => ({ ...current, loading: true, status: "", result: "" }));
    try {
      const result = await api.admin.testApiToken(mode.path, value);
      setTokenTest(current => ({
        ...current,
        loading: false,
        status: "Доступ дозволено",
        result: formatApiResult(result)
      }));
    } catch (error) {
      setTokenTest(current => ({
        ...current,
        loading: false,
        status: "Доступ заборонено або token відкликано",
        result: error.message
      }));
    }
  }

  async function revokeToken(token) {
    if (token.revokedAt || !window.confirm(`Відкликати API token "${token.name}"?`)) return;
    try {
      await api.admin.revokeApiToken(token.id);
      await reloadAdmin();
      showToast("API token відкликано", "Подальші запити з цим токеном будуть відхилені.");
    } catch (error) {
      showToast("Token не відкликано", error.message, "danger");
    }
  }

  async function deleteToken(token) {
    if (!window.confirm(`Повністю видалити API token "${token.name}"? Запис зникне зі списку.`)) return;
    try {
      await api.admin.deleteApiToken(token.id);
      await reloadAdmin();
      showToast("API token видалено", "Запис повністю прибрано з бази.");
    } catch (error) {
      showToast("Token не видалено", error.message, "danger");
    }
  }

  async function runParser() {
    try {
      await api.admin.runParser();
      await reloadFuelData();
      showToast("Парсер запущено", "Дані Minfin оновлюються на backend.");
    } catch (error) {
      showToast("Парсер не запущено", error.message, "danger");
    }
  }

  return (
    <Section title="Адмін-панель" subtitle="Керування цінами, АЗС, фото, сайтами операторів, API токенами, користувачами та коментарями." className="max-w-[96rem]">
      <div className="mb-5 flex flex-wrap gap-2">
        {[
          ["prices", "Ціни"],
          ["stations", "АЗС"],
          ["tokens", "API токени"],
          ["users", "Користувачі"],
          ["comments", "Коментарі"]
        ].map(([id, label]) => (
          <button key={id} onClick={() => setTab(id)} className={`rounded-full px-4 py-2 text-sm font-bold transition ${tab === id ? "bg-brand-600 text-white" : "bg-slate-100 text-slate-700 dark:bg-slate-900 dark:text-slate-200"}`}>{label}</button>
        ))}
        <Button variant="secondary" onClick={runParser}><Play className="h-4 w-4" /> Запустити парсер</Button>
      </div>

      {tab === "prices" && (
        <Card as="form" onSubmit={savePrice} className="grid gap-4">
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
            <Select label="АЗС" value={price.stationId} onChange={event => setPrice({ ...price, stationId: event.target.value })} required>
              <option value="">Оберіть АЗС</option>
              {stations.map(station => <option key={station.id} value={station.id}>{station.name} · {station.address}</option>)}
            </Select>
            <Select label="Пальне" value={price.fuelId} onChange={event => setPrice({ ...price, fuelId: event.target.value })} required>
              <option value="">Оберіть пальне</option>
              {fuels.map(fuel => <option key={fuel.id} value={fuel.id}>{fuel.name}</option>)}
            </Select>
            <Input type="number" step="0.01" label="Нова ціна" value={price.price} onChange={event => setPrice({ ...price, price: event.target.value })} required />
            <Input type="number" label="Популярність" value={price.popularity} onChange={event => setPrice({ ...price, popularity: event.target.value })} />
            <Input type="date" label="Дата" value={price.date} onChange={event => setPrice({ ...price, date: event.target.value })} required />
          </div>
          <div className="soft-panel">Поточна ціна: <strong>{currentPrice ? `${formatPrice(currentPrice.price)} грн` : "немає"}</strong></div>
          <Button className="w-fit"><Save className="h-4 w-4" /> Зберегти ціну</Button>
        </Card>
      )}

      {tab === "stations" && (
        <Card as="form" onSubmit={saveStation} className="grid gap-4">
          <Select label="Завантажити існуючу АЗС" value={stationForm.id} onChange={event => loadStation(event.target.value)}>
            <option value="">Нова АЗС</option>
            {stations.map(station => <option key={station.id} value={station.id}>{station.name} · {station.address}</option>)}
          </Select>
          <div className="grid gap-4 md:grid-cols-2">
            <Input label="Назва" value={stationForm.name} onChange={event => setStationForm({ ...stationForm, name: event.target.value })} required />
            <Input label="Місто" value={stationForm.city} onChange={event => setStationForm({ ...stationForm, city: event.target.value })} required />
            <Input label="Адреса" value={stationForm.address} onChange={event => setStationForm({ ...stationForm, address: event.target.value })} required />
            <Input label="Головне фото" value={stationForm.imageUrl} onChange={event => setStationForm({ ...stationForm, imageUrl: event.target.value })} />
            <Input label="Сайт АЗС" value={stationForm.websiteUrl} onChange={event => setStationForm({ ...stationForm, websiteUrl: event.target.value })} />
            <Input label="Latitude" value={stationForm.latitude} onChange={event => setStationForm({ ...stationForm, latitude: event.target.value })} required />
            <Input label="Longitude" value={stationForm.longitude} onChange={event => setStationForm({ ...stationForm, longitude: event.target.value })} required />
          </div>
          <Textarea label="Фото для задньої сторони картки, по одному URL на рядок" value={stationForm.photoUrls} onChange={event => setStationForm({ ...stationForm, photoUrls: event.target.value })} />
          <div className="flex gap-2">
            <Button><Save className="h-4 w-4" /> Зберегти АЗС</Button>
            {stationForm.id && <Button type="button" variant="danger" onClick={deleteStation}><Trash2 className="h-4 w-4" /> Видалити</Button>}
          </div>
        </Card>
      )}

      {tab === "tokens" && (
        <div className="grid gap-5 xl:grid-cols-[minmax(320px,380px)_minmax(0,1fr)]">
          <div className="grid min-w-0 content-start gap-5">
            <Card as="form" onSubmit={createToken} className="grid gap-4">
              <div>
                <h3 className="text-lg font-black">Створити API-токен</h3>
                <p className="mt-1 text-sm text-slate-500">Raw-token показується тільки одразу після створення. Старі токени в базі зберігаються лише як hash.</p>
              </div>
              <Input label="Назва інтеграції" value={tokenForm.name} onChange={event => setTokenForm({ ...tokenForm, name: event.target.value })} maxLength={100} required />
              <div>
                <div className="mb-2 text-sm font-semibold text-slate-700 dark:text-slate-200">Один рівень доступу</div>
                <div className="grid gap-2">
                  {scopeOptions.map(scope => {
                    const selected = tokenForm.scopes === scope.value;
                    return (
                      <button
                        key={scope.value}
                        type="button"
                        title={`${scope.title}: ${scope.text}`}
                        onClick={() => setTokenForm(current => ({ ...current, scopes: scope.value }))}
                        className={`rounded-xl border p-3 text-left text-sm transition hover:-translate-y-0.5 ${
                          selected
                            ? "border-brand-300 bg-brand-50 text-brand-900 shadow-sm dark:border-brand-500/40 dark:bg-brand-500/10 dark:text-brand-50"
                            : "border-slate-200 bg-slate-50 text-slate-700 dark:border-slate-800 dark:bg-slate-900 dark:text-slate-200"
                        }`}
                      >
                        <span className="flex items-start justify-between gap-3">
                          <span>
                            <strong className="block">{scope.value} — {scope.title}</strong>
                            <span className="mt-1 block text-xs text-slate-500 dark:text-slate-400">{scope.text}</span>
                          </span>
                          {selected && <span className="badge-soft shrink-0">обрано</span>}
                        </span>
                      </button>
                    );
                  })}
                </div>
                <p className="mt-2 text-xs text-slate-500">Потрібен тільки один preset. Для write-доступу оберіть окремий write preset або комбінований read+write.</p>
              </div>
              <Input type="datetime-local" label="Діє до" value={tokenForm.expiresAt} onChange={event => setTokenForm({ ...tokenForm, expiresAt: event.target.value })} />
              <Button><KeyRound className="h-4 w-4" /> Створити token</Button>

              {createdToken && (
                <div className="rounded-2xl border border-emerald-200 bg-emerald-50/80 p-4 text-sm text-emerald-900 dark:border-emerald-500/30 dark:bg-emerald-500/10 dark:text-emerald-100">
                  <div className="mb-2 font-bold">Скопіюйте токен зараз</div>
                  <div className="break-all rounded-xl bg-white/80 p-3 font-mono text-xs text-slate-900 dark:bg-slate-950/80 dark:text-slate-100">
                    {showCreatedToken ? createdToken.token : maskToken(createdToken.token)}
                  </div>
                  <div className="mt-3 flex flex-wrap gap-2">
                    <Button type="button" variant="secondary" onClick={() => setShowCreatedToken(value => !value)}>
                      {showCreatedToken ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                      {showCreatedToken ? "Сховати" : "Показати"}
                    </Button>
                    <Button type="button" variant="secondary" onClick={copyCreatedToken}><Copy className="h-4 w-4" /> Скопіювати</Button>
                    <Button type="button" variant="ghost" onClick={() => setTokenTest(current => ({ ...current, value: createdToken.token }))}>Вставити в перевірку</Button>
                  </div>
                </div>
              )}
            </Card>

            <Card className="grid min-w-0 max-w-full gap-4 overflow-hidden">
              <div className="min-w-0">
                <h3 className="text-lg font-black">Перевірити API-токен</h3>
                <p className="mt-1 text-sm text-slate-500">
                  Перевірка йде через external API з header{" "}
                  <code className="rounded bg-slate-100 px-1 py-0.5 dark:bg-slate-950">X-API-Token</code>.
                  Відкликаний token має повернути помилку доступу.
                </p>
              </div>

              <Input label="API token" value={tokenTest.value} onChange={event => setTokenTest(current => ({ ...current, value: event.target.value }))} placeholder="lfm_..." />

              <Select label="Перевірка доступу" value={tokenTest.mode} onChange={event => setTokenTest(current => ({ ...current, mode: event.target.value }))}>
                {tokenTestModes.map(mode => <option key={mode.value} value={mode.value}>{mode.label}</option>)}
              </Select>

              <Button type="button" variant="secondary" loading={tokenTest.loading} onClick={testApiToken}>
                <ShieldCheck className="h-4 w-4" /> Перевірити доступ
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
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <h3 className="text-lg font-black">Активні та відкликані токени</h3>
                <p className="mt-1 text-sm text-slate-500">Revoke блокує доступ, повне видалення прибирає запис зі списку.</p>
              </div>
              <Button type="button" variant="secondary" onClick={reloadAdmin}><RefreshCw className="h-4 w-4" /> Оновити</Button>
            </div>
            <div className="table-wrap overflow-hidden">
              <table className="data-table table-fixed">
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
                    <th>Назва</th>
                    <th>Scope</th>
                    <th>Статус</th>
                    <th>Створено</th>
                    <th>Діє до</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {tokens.length === 0 && (
                    <tr>
                      <td colSpan={6} className="text-center text-sm text-slate-500">API-токенів ще немає.</td>
                    </tr>
                  )}
                  {tokens.map(token => {
                    const revoked = Boolean(token.revokedAt);
                    const expired = token.expiresAt && new Date(token.expiresAt) <= new Date();
                    return (
                      <tr key={token.id}>
                        <td>
                          <strong title={token.name} className="block truncate">{truncateTokenName(token.name)}</strong>
                          <div title={token.createdByEmail} className="truncate text-xs text-slate-500">{token.createdByEmail}</div>
                        </td>
                        <td><code title={token.scopes} className="block truncate rounded bg-slate-100 px-2 py-1 text-xs dark:bg-slate-950">{token.scopes}</code></td>
                        <td>
                          <span className={`inline-flex max-w-full rounded-full px-2 py-1 text-xs font-bold ${
                            revoked
                              ? "bg-rose-50 text-rose-700 dark:bg-rose-500/10 dark:text-rose-200"
                              : expired
                                ? "bg-amber-50 text-amber-700 dark:bg-amber-500/10 dark:text-amber-200"
                                : "bg-emerald-50 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-200"
                          }`}>
                            {revoked ? "відкликано" : expired ? "прострочено" : "активний"}
                          </span>
                          {revoked && <div className="mt-1 truncate text-xs text-slate-500">{formatDateTime(token.revokedAt)}</div>}
                        </td>
                        <td className="text-xs leading-5">{formatDateTime(token.createdAt)}</td>
                        <td className="text-xs leading-5">{token.expiresAt ? formatDateTime(token.expiresAt) : "безстроково"}</td>
                        <td>
                          <div className="grid gap-2">
                            <Button type="button" variant="secondary" className="min-h-9 w-full px-2 py-1.5 text-xs" disabled={revoked} onClick={() => revokeToken(token)}><Ban className="h-3.5 w-3.5" /> Відкликати</Button>
                            <Button type="button" variant="danger" className="min-h-9 w-full px-2 py-1.5 text-xs" onClick={() => deleteToken(token)}><Trash2 className="h-3.5 w-3.5" /> Видалити</Button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </Card>
        </div>
      )}

      {tab === "users" && (
        <Card>
          <div className="table-wrap overflow-x-auto">
            <table className="data-table">
              <thead><tr><th>Email</th><th>Нікнейм</th><th>Роль</th><th>Підтверджено</th><th></th></tr></thead>
              <tbody>{users.map(user => (
                <tr key={user.id}>
                  <td>{user.email}</td><td>{user.nickname}</td>
                  <td><Select value={user.role} onChange={event => api.admin.updateUserRole(user.id, event.target.value).then(reloadAdmin)}><option>User</option><option>Admin</option></Select></td>
                  <td>{user.emailConfirmed ? "так" : "ні"}</td>
                  <td><Button variant="danger" onClick={() => api.admin.deleteUser(user.id).then(reloadAdmin)}><UserCog className="h-4 w-4" /> Видалити</Button></td>
                </tr>
              ))}</tbody>
            </table>
          </div>
        </Card>
      )}

      {tab === "comments" && (
        <Card>
          <div className="grid gap-3">
            {comments.map(comment => (
              <div key={comment.id} className="rounded-2xl border border-slate-200 p-4 dark:border-slate-800">
                <div className="font-bold">{comment.authorName} · {comment.stationName}</div>
                <p className="mt-2 text-sm">{comment.content}</p>
                <div className="mt-3 flex gap-2"><Button variant="danger" onClick={() => api.admin.deleteComment(comment.id).then(reloadAdmin)}>Видалити</Button></div>
              </div>
            ))}
          </div>
        </Card>
      )}
    </Section>
  );
}