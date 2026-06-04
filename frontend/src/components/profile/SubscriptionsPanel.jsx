import { useEffect, useMemo, useState } from "react";
import { BellRing, CalendarClock, Pencil, Power, Save, Trash2, X } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Button, Input, Select } from "../common/Ui";
import { api } from "../../services/api";
import { useData } from "../../contexts/DataContext";
import { useToast } from "../../contexts/ToastContext";
import { cx } from "../../utils/format";

const frequencyValues = ["Immediate", "Daily", "Weekly"];

function currentTimeValue() {
  const now = new Date();
  return `${String(now.getHours()).padStart(2, "0")}:${String(now.getMinutes()).padStart(2, "0")}`;
}

function createInitialForm() {
  return {
    fuelIds: [],
    city: "Харків",
    frequency: "Immediate",
    sendTime: currentTimeValue(),
    isActive: true
  };
}

export default function SubscriptionsPanel() {
  const { t } = useTranslation();
  const { fuels } = useData();
  const { showToast } = useToast();
  const [items, setItems] = useState([]);
  const [form, setForm] = useState(() => createInitialForm());
  const [editingId, setEditingId] = useState(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  const frequencyOptions = useMemo(
    () => frequencyValues.map(value => ({
      value,
      label: t(`subscriptions.frequencies.${value}`),
      description: t(`subscriptions.frequencyDescriptions.${value}`)
    })),
    [t]
  );

  const selectedFrequency = useMemo(
    () => frequencyOptions.find(option => option.value === form.frequency) || frequencyOptions[0],
    [form.frequency, frequencyOptions]
  );

  const frequencyLabel = value => t(`subscriptions.frequencies.${value}`, { defaultValue: value });

  async function load() {
    setLoading(true);
    try {
      setItems(await api.subscriptions.list());
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    load().catch(() => {});
  }, []);

  function resetForm() {
    setEditingId(null);
    setForm(createInitialForm());
  }

  function toggleFuel(fuelId) {
    setForm(previous => {
      const exists = previous.fuelIds.includes(fuelId);
      return {
        ...previous,
        fuelIds: exists
          ? previous.fuelIds.filter(id => id !== fuelId)
          : [...previous.fuelIds, fuelId]
      };
    });
  }

  function startEdit(item) {
    setEditingId(item.id);
    setForm({
      fuelIds: [item.fuelId],
      city: item.city || "Харків",
      frequency: item.frequency || "Immediate",
      sendTime: (item.sendTime || currentTimeValue()).slice(0, 5),
      isActive: item.isActive !== false
    });
  }

  function buildPayload() {
    return {
      city: form.city.trim(),
      frequency: form.frequency,
      sendTime: form.sendTime,
      isActive: form.isActive,
      ...(editingId
        ? { fuelId: Number(form.fuelIds[0]) }
        : { fuelIds: form.fuelIds.map(Number) })
    };
  }

  async function submit(event) {
    event.preventDefault();
    if (form.fuelIds.length === 0) {
      showToast(t("subscriptions.chooseFuelTitle"), t("subscriptions.chooseFuelMessage"), "warning");
      return;
    }

    setSaving(true);
    try {
      const payload = buildPayload();
      if (editingId) {
        await api.subscriptions.update(editingId, payload);
      } else {
        await api.subscriptions.create(payload);
      }

      showToast(
        t("subscriptions.savedTitle"),
        t("subscriptions.savedMessage")
      );
      resetForm();
      await load();
    } catch (error) {
      showToast(t("subscriptions.saveFailed"), error.message || t("auth.actionFailed"), "danger");
    } finally {
      setSaving(false);
    }
  }

  async function toggleActive(item) {
    try {
      await api.subscriptions.update(item.id, {
        fuelId: item.fuelId,
        city: item.city,
        frequency: item.frequency,
        sendTime: item.sendTime,
        isActive: !item.isActive
      });
      showToast(
        !item.isActive ? t("subscriptions.toggleOn") : t("subscriptions.toggleOff"),
        !item.isActive ? t("subscriptions.toggleOnMessage") : t("subscriptions.toggleOffMessage")
      );
      await load();
    } catch (error) {
      showToast(t("subscriptions.statusFailed"), error.message || t("auth.actionFailed"), "danger");
    }
  }

  async function remove(id) {
    try {
      await api.subscriptions.delete(id);
      showToast(t("subscriptions.deleted"), t("subscriptions.deletedMessage"));
      await load();
      if (editingId === id) resetForm();
    } catch (error) {
      showToast(t("subscriptions.deleteFailed"), error.message || t("auth.actionFailed"), "danger");
    }
  }

  return (
    <div className="grid gap-4">
      {items.length === 0 && !loading && (
        <div className="panel grid gap-3 border-brand-200 bg-brand-50/70 p-5 text-sm text-slate-700 dark:border-lime-300/30 dark:bg-slate-900 dark:text-slate-200">
          <div className="flex items-start gap-3">
            <BellRing className="mt-0.5 h-5 w-5 shrink-0 text-brand-700 dark:text-lime-300" />
            <div>
              <h3 className="font-bold text-slate-950 dark:text-white">{t("subscriptions.ctaTitle")}</h3>
              <p className="mt-1 leading-6 text-slate-600 dark:text-slate-300">
                {t("subscriptions.ctaText")}
              </p>
            </div>
          </div>
        </div>
      )}

      <form onSubmit={submit} className="panel grid gap-4 p-5">
        <div className="flex items-start justify-between gap-3">
          <div>
            <h3 className="font-bold text-slate-950 dark:text-white">{editingId ? t("subscriptions.edit") : t("subscriptions.create")}</h3>
            <p className="mt-1 text-sm leading-6 text-slate-500 dark:text-slate-300">
              {t("subscriptions.formHint")}
            </p>
          </div>
          {editingId && (
            <button
              type="button"
              onClick={resetForm}
              className="grid min-h-10 min-w-10 place-items-center rounded-full text-slate-500 transition hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800"
              aria-label={t("common.cancel")}
            >
              <X className="h-4 w-4" />
            </button>
          )}
        </div>

        <div>
          <span className="mb-2 block text-sm font-semibold text-slate-700 dark:text-slate-200">{t("subscriptions.fuelTypes")}</span>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
            {fuels.map(fuel => {
              const checked = form.fuelIds.includes(fuel.id);
              return (
                <label
                  key={fuel.id}
                  className={cx(
                    "flex min-h-11 cursor-pointer items-center gap-2 rounded-xl border px-3 py-2 text-sm font-semibold transition",
                    checked
                      ? "border-brand-500 bg-brand-50 text-brand-800 dark:border-lime-300 dark:bg-lime-300/10 dark:text-lime-100"
                      : "border-slate-200 bg-white text-slate-700 hover:border-brand-300 dark:border-slate-800 dark:bg-slate-950 dark:text-slate-200"
                  )}
                >
                  <input
                    type="checkbox"
                    className="h-4 w-4 accent-brand-600"
                    checked={checked}
                    disabled={Boolean(editingId) && !checked}
                    onChange={() => toggleFuel(fuel.id)}
                  />
                  {fuel.name}
                </label>
              );
            })}
          </div>
          {editingId && <p className="mt-2 text-xs text-slate-500 dark:text-slate-400">{t("subscriptions.editFuelHint")}</p>}
        </div>

        <div className="grid gap-3">
          <Input
            label={t("common.city")}
            value={form.city}
            onChange={event => setForm({ ...form, city: event.target.value })}
            required
          />
        </div>

        <div className="grid gap-3 sm:grid-cols-[1fr_150px]">
          <Select
            label={t("subscriptions.frequency")}
            value={form.frequency}
            onChange={event => setForm({ ...form, frequency: event.target.value })}
          >
            {frequencyOptions.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
          </Select>
          <Input
            type="time"
            label={t("subscriptions.sendTime")}
            value={form.sendTime}
            onChange={event => setForm({ ...form, sendTime: event.target.value })}
          />
        </div>

        <div className="rounded-xl border border-slate-200 bg-slate-50 p-3 text-sm leading-6 text-slate-600 dark:border-slate-800 dark:bg-slate-900 dark:text-slate-300">
          <CalendarClock className="mr-2 inline h-4 w-4 text-brand-700 dark:text-lime-300" />
          {selectedFrequency.description}
        </div>

        <label className="flex items-center gap-2 text-sm font-semibold text-slate-700 dark:text-slate-200">
          <input
            type="checkbox"
            className="h-4 w-4 accent-brand-600"
            checked={form.isActive}
            onChange={event => setForm({ ...form, isActive: event.target.checked })}
          />
          {t("subscriptions.active")}
        </label>

        <Button loading={saving}>
          <Save className="h-4 w-4" /> {editingId ? t("common.saveChanges") : t("subscriptions.create")}
        </Button>
      </form>

      <div className="panel p-5">
        <h3 className="font-bold text-slate-950 dark:text-white">{t("subscriptions.my")}</h3>
        <div className="mt-3 grid max-h-[420px] gap-2 overflow-y-auto pr-1">
          {loading && <p className="text-sm text-slate-500 dark:text-slate-300">{t("subscriptions.loading")}</p>}
          {!loading && items.length === 0 && <p className="text-sm text-slate-500 dark:text-slate-300">{t("subscriptions.empty")}</p>}
          {items.map(item => (
            <div
              key={item.id}
              className={cx(
                "rounded-xl border p-3 text-sm transition dark:border-slate-800",
                item.isActive ? "border-slate-200 bg-white dark:bg-slate-950" : "border-slate-200 bg-slate-50 opacity-75 dark:bg-slate-900"
              )}
            >
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div className="min-w-0">
                  <div className="font-bold text-slate-950 dark:text-white">{item.fuelName} · {item.city}</div>
                  <div className="mt-1 text-slate-500 dark:text-slate-300">
                    {frequencyLabel(item.frequency)} · {String(item.sendTime || "").slice(0, 5)}
                  </div>
                  <div className="mt-2">
                    <span className={item.isActive ? "badge-soft bg-emerald-50 text-emerald-700" : "badge-soft bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300"}>
                      {item.isActive ? t("common.active") : t("common.disabled")}
                    </span>
                  </div>
                </div>
                <div className="grid grid-cols-3 gap-2 sm:flex">
                  <Button type="button" variant="secondary" onClick={() => startEdit(item)} title={t("common.edit")}>
                    <Pencil className="h-4 w-4" />
                  </Button>
                  <Button type="button" variant="secondary" onClick={() => toggleActive(item)} title={item.isActive ? t("common.disabled") : t("common.active")}>
                    <Power className="h-4 w-4" />
                  </Button>
                  <Button type="button" variant="danger" onClick={() => remove(item.id)} title={t("common.delete")}>
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
