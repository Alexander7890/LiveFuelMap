import { ChevronLeft, ChevronRight, Edit3, MessageSquarePlus, Trash2 } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button, Card, Select, Textarea } from "../common/Ui";
import Stars from "../common/Stars";
import { useAuth } from "../../contexts/AuthContext";
import { useData } from "../../contexts/DataContext";
import { useToast } from "../../contexts/ToastContext";
import { formatDateTime } from "../../utils/format";

const COMMENTS_PAGE_SIZE = 6;

export default function CommentsSection({ stationFilter, setStationFilter }) {
  const { t } = useTranslation();
  const { currentUser, isLoggedIn } = useAuth();
  const { stations, fuels, comments, sendComment, updateOwnComment, deleteOwnComment } = useData();
  const { showToast } = useToast();
  const [formOpen, setFormOpen] = useState(false);
  const [form, setForm] = useState({ stationId: "", fuelId: "", content: "", rating: 0 });
  const [editing, setEditing] = useState(null);
  const [page, setPage] = useState(1);
  const commentsListRef = useRef(null);

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

  const totalPages = Math.max(1, Math.ceil(visible.length / COMMENTS_PAGE_SIZE));
  const safePage = Math.min(page, totalPages);
  const pageStart = visible.length === 0 ? 0 : (safePage - 1) * COMMENTS_PAGE_SIZE + 1;
  const pageEnd = Math.min(safePage * COMMENTS_PAGE_SIZE, visible.length);
  const pagedComments = visible.slice((safePage - 1) * COMMENTS_PAGE_SIZE, safePage * COMMENTS_PAGE_SIZE);

  useEffect(() => {
    setPage(1);
  }, [stationFilter]);

  useEffect(() => {
    if (page > totalPages) setPage(totalPages);
  }, [page, totalPages]);

  async function submit(event) {
    event.preventDefault();
    if (!isLoggedIn) {
      showToast(t("comments.authRequiredTitle"), t("comments.authRequiredMessage"), "warning");
      return;
    }
    if (!form.rating) {
      showToast(t("comments.ratingRequiredTitle"), t("comments.ratingRequiredMessage"), "warning");
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
      setPage(1);
      showToast(t("comments.sentTitle"), t("comments.sentMessage"));
    } catch (error) {
      showToast(t("comments.notSent"), error.message, "danger");
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
      showToast(t("comments.updatedTitle"), t("comments.updatedMessage"));
    } catch (error) {
      showToast(t("comments.updateFailed"), error.message, "danger");
    }
  }

  async function remove(comment) {
    if (!window.confirm(t("comments.deleteConfirm"))) return;
    try {
      await deleteOwnComment(comment.id);
      showToast(t("comments.deletedTitle"), t("comments.deletedMessage"));
    } catch (error) {
      showToast(t("comments.deleteFailed"), error.message, "danger");
    }
  }

  function goToPage(nextPage) {
    setPage(Math.min(totalPages, Math.max(1, nextPage)));
  }

  function handleCommentsWheel(event) {
    const container = commentsListRef.current;
    if (!container) return;

    const canScroll = container.scrollHeight > container.clientHeight;
    const atTop = container.scrollTop <= 0;
    const atBottom = container.scrollTop + container.clientHeight >= container.scrollHeight - 1;

    event.stopPropagation();

    if (!canScroll || (event.deltaY < 0 && atTop) || (event.deltaY > 0 && atBottom)) {
      event.preventDefault();
    }
  }

  return (
    <Card>
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h3 className="text-lg font-black">{t("comments.title")}</h3>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            {t("comments.subtitle")}
          </p>
        </div>
        <div className="grid gap-2 sm:flex sm:flex-wrap">
          <Select value={stationFilter || ""} onChange={event => setStationFilter(event.target.value ? Number(event.target.value) : null)}>
            <option value="">{t("comments.allStations")}</option>
            {stations.map(station => <option key={station.id} value={station.id}>{station.name}</option>)}
          </Select>
          <Button onClick={() => setFormOpen(value => !value)}><MessageSquarePlus className="h-4 w-4" /> {t("comments.new")}</Button>
        </div>
      </div>

      {formOpen && (
        <form onSubmit={submit} className="mt-5 grid gap-4 rounded-2xl border border-brand-200 bg-brand-50/60 p-4 dark:border-brand-800 dark:bg-brand-500/10">
          <div className="grid gap-4 md:grid-cols-2">
            <Select label={t("common.stations")} value={form.stationId} onChange={event => setForm({ ...form, stationId: event.target.value, fuelId: "" })} required>
              <option value="">{t("comments.chooseStation")}</option>
              {stations.map(station => <option key={station.id} value={station.id}>{station.name}</option>)}
            </Select>
            <Select label={t("common.fuel")} value={form.fuelId} onChange={event => setForm({ ...form, fuelId: event.target.value })}>
              <option value="">{t("comments.noFuelBinding")}</option>
              {availableFuels.map(fuel => <option key={fuel.id} value={fuel.id}>{fuel.name}</option>)}
            </Select>
          </div>
          <div>
            <div className="mb-2 text-sm font-semibold">{t("common.rating")}</div>
            <Stars interactive rating={form.rating} onChange={rating => setForm({ ...form, rating })} />
          </div>
          <Textarea label={t("comments.comment")} value={form.content} onChange={event => setForm({ ...form, content: event.target.value })} maxLength={2000} required />
          <Button>{t("common.send")}</Button>
        </form>
      )}

      <div className="mt-5 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="text-sm font-semibold text-slate-600 dark:text-slate-300">
          {t("common.found")}: {visible.length} · {t("common.shown")} {pageStart}-{pageEnd}
        </div>
        {totalPages > 1 && (
          <div className="flex items-center gap-2">
            <Button type="button" variant="secondary" className="min-h-10 px-3" disabled={safePage <= 1} onClick={() => goToPage(safePage - 1)}>
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <span className="rounded-xl border border-slate-200 px-3 py-2 text-sm font-bold text-slate-700 dark:border-slate-800 dark:text-slate-200">
              {safePage} / {totalPages}
            </span>
            <Button type="button" variant="secondary" className="min-h-10 px-3" disabled={safePage >= totalPages} onClick={() => goToPage(safePage + 1)}>
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        )}
      </div>

      <div
        ref={commentsListRef}
        className="mt-3 max-h-[720px] overflow-y-auto overscroll-contain pr-1"
        onWheel={handleCommentsWheel}
      >
        <div className="grid gap-3">
          {visible.length === 0 && <div className="rounded-2xl bg-slate-50 p-5 text-sm text-slate-500 dark:bg-slate-900 dark:text-slate-300">{t("comments.empty")}</div>}
          {pagedComments.map(comment => {
            const mine = currentUser && Number(currentUser.userId) === Number(comment.userId);
            const isEditing = editing?.id === comment.id;
            return (
              <article key={comment.id} className="rounded-2xl border border-slate-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-950">
                <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                  <div>
                    <div className="font-bold">{comment.authorName}</div>
                    <div className="text-sm text-slate-500 dark:text-slate-400">
                      {comment.stationName}{comment.fuelName ? ` · ${comment.fuelName}` : ""} · {formatDateTime(comment.createdAt)}
                    </div>
                  </div>
                  <Stars rating={comment.rating} />
                </div>
                {isEditing ? (
                  <div className="mt-3 grid gap-3">
                    <Textarea value={editing.content} onChange={event => setEditing({ ...editing, content: event.target.value })} />
                    <Stars interactive rating={editing.rating} onChange={rating => setEditing({ ...editing, rating })} />
                    <div className="grid gap-2 sm:flex">
                      <Button onClick={() => saveEdit(comment)} type="button">{t("common.save")}</Button>
                      <Button variant="secondary" onClick={() => setEditing(null)} type="button">{t("common.cancel")}</Button>
                    </div>
                  </div>
                ) : (
                  <p className="mt-3 text-sm leading-6 text-slate-700 dark:text-slate-200">{comment.content}</p>
                )}
                {mine && !isEditing && (
                  <div className="mt-3 grid gap-2 sm:flex">
                    <Button variant="secondary" onClick={() => setEditing({ id: comment.id, content: comment.content, rating: comment.rating })}><Edit3 className="h-4 w-4" /> {t("common.edit")}</Button>
                    <Button variant="danger" onClick={() => remove(comment)}><Trash2 className="h-4 w-4" /> {t("common.delete")}</Button>
                  </div>
                )}
              </article>
            );
          })}
        </div>
      </div>
    </Card>
  );
}
