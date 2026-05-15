import { Edit3, MessageSquarePlus, Trash2 } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { Button, Card, Select, Textarea } from "../common/Ui";
import Stars from "../common/Stars";
import { useAuth } from "../../contexts/AuthContext";
import { useData } from "../../contexts/DataContext";
import { useToast } from "../../contexts/ToastContext";
import { formatDateTime } from "../../utils/format";

export default function CommentsSection({ stationFilter, setStationFilter }) {
  const { currentUser, isLoggedIn } = useAuth();
  const { stations, fuels, comments, sendComment, updateOwnComment, deleteOwnComment } = useData();
  const { showToast } = useToast();
  const [formOpen, setFormOpen] = useState(false);
  const [form, setForm] = useState({ stationId: "", fuelId: "", content: "", rating: 0 });
  const [editing, setEditing] = useState(null);

  useEffect(() => {
    if (stationFilter) setForm(current => ({ ...current, stationId: String(stationFilter) }));
  }, [stationFilter]);

  const selectedStation = stations.find(item => Number(item.id) === Number(form.stationId));
  const availableFuels = selectedStation
    ? fuels.filter(fuel => selectedStation.prices?.some(price => price.fuelId === fuel.id))
    : fuels;

  const visible = useMemo(() => {
    return [...comments]
      .filter(item => !stationFilter || Number(item.stationId) === Number(stationFilter))
      .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));
  }, [comments, stationFilter]);

  async function submit(event) {
    event.preventDefault();
    if (!isLoggedIn) {
      showToast("Потрібна авторизація", "Увійдіть, щоб залишити відгук.", "warning");
      return;
    }
    if (!form.rating) {
      showToast("Оберіть рейтинг", "Поставте від 1 до 5 зірок.", "warning");
      return;
    }
    try {
      await sendComment({
        stationId: Number(form.stationId),
        fuelId: form.fuelId ? Number(form.fuelId) : null,
        content: form.content,
        rating: Number(form.rating)
      });
      setForm({ stationId: form.stationId, fuelId: "", content: "", rating: 0 });
      setFormOpen(false);
      showToast("Відгук надіслано", "Коментар з'явиться у всіх користувачів через WebSocket.");
    } catch (error) {
      showToast("Коментар не надіслано", error.message, "danger");
    }
  }

  async function saveEdit(comment) {
    try {
      const next = {
        content: editing.content,
        rating: Number(editing.rating)
      };
      await updateOwnComment(comment.id, next);
      setEditing(null);
      showToast("Коментар оновлено", "Ваш відгук змінено.");
    } catch (error) {
      showToast("Не вдалося оновити", error.message, "danger");
    }
  }

  async function remove(comment) {
    if (!window.confirm("Видалити ваш коментар?")) return;
    try {
      await deleteOwnComment(comment.id);
      showToast("Коментар видалено", "Ваш відгук прибрано.");
    } catch (error) {
      showToast("Не вдалося видалити", error.message, "danger");
    }
  }

  return (
    <Card>
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h3 className="text-lg font-black">Відгуки користувачів</h3>
          <p className="text-sm text-slate-500">Усі відгуки видно без авторизації, створення доступне тільки користувачам.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Select value={stationFilter || ""} onChange={event => setStationFilter(event.target.value ? Number(event.target.value) : null)}>
            <option value="">Усі АЗС</option>
            {stations.map(station => <option key={station.id} value={station.id}>{station.name}</option>)}
          </Select>
          <Button onClick={() => setFormOpen(value => !value)}><MessageSquarePlus className="h-4 w-4" /> Новий відгук</Button>
        </div>
      </div>

      {formOpen && (
        <form onSubmit={submit} className="mt-5 grid gap-4 rounded-2xl border border-brand-200 bg-brand-50/60 p-4 dark:border-brand-800 dark:bg-brand-500/10">
          <div className="grid gap-4 md:grid-cols-2">
            <Select label="АЗС" value={form.stationId} onChange={event => setForm({ ...form, stationId: event.target.value, fuelId: "" })} required>
              <option value="">Оберіть АЗС</option>
              {stations.map(station => <option key={station.id} value={station.id}>{station.name}</option>)}
            </Select>
            <Select label="Пальне на цій АЗС" value={form.fuelId} onChange={event => setForm({ ...form, fuelId: event.target.value })}>
              <option value="">Без прив'язки</option>
              {availableFuels.map(fuel => <option key={fuel.id} value={fuel.id}>{fuel.name}</option>)}
            </Select>
          </div>
          <div>
            <div className="mb-2 text-sm font-semibold">Оцінка</div>
            <Stars interactive rating={form.rating} onChange={rating => setForm({ ...form, rating })} />
          </div>
          <Textarea label="Повідомлення" value={form.content} onChange={event => setForm({ ...form, content: event.target.value })} maxLength={2000} required />
          <Button>Надіслати</Button>
        </form>
      )}

      <div className="mt-5 grid gap-3">
        {visible.length === 0 && <div className="rounded-2xl bg-slate-50 p-5 text-sm text-slate-500 dark:bg-slate-900">Коментарів немає.</div>}
        {visible.map(comment => {
          const mine = currentUser && Number(currentUser.userId) === Number(comment.userId);
          const isEditing = editing?.id === comment.id;
          return (
            <article key={comment.id} className="rounded-2xl border border-slate-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-950">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <div className="font-bold">{comment.authorName}</div>
                  <div className="text-sm text-slate-500">{comment.stationName}{comment.fuelName ? ` · ${comment.fuelName}` : ""} · {formatDateTime(comment.createdAt)}</div>
                </div>
                <Stars rating={comment.rating} />
              </div>
              {isEditing ? (
                <div className="mt-3 grid gap-3">
                  <Textarea value={editing.content} onChange={event => setEditing({ ...editing, content: event.target.value })} />
                  <Stars interactive rating={editing.rating} onChange={rating => setEditing({ ...editing, rating })} />
                  <div className="flex gap-2">
                    <Button onClick={() => saveEdit(comment)} type="button">Зберегти</Button>
                    <Button variant="secondary" onClick={() => setEditing(null)} type="button">Скасувати</Button>
                  </div>
                </div>
              ) : (
                <p className="mt-3 text-sm leading-6 text-slate-700 dark:text-slate-200">{comment.content}</p>
              )}
              {mine && !isEditing && (
                <div className="mt-3 flex gap-2">
                  <Button variant="secondary" onClick={() => setEditing({ id: comment.id, content: comment.content, rating: comment.rating })}><Edit3 className="h-4 w-4" /> Редагувати</Button>
                  <Button variant="danger" onClick={() => remove(comment)}><Trash2 className="h-4 w-4" /> Видалити</Button>
                </div>
              )}
            </article>
          );
        })}
      </div>
    </Card>
  );
}

