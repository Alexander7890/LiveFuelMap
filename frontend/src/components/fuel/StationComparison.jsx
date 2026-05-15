import { Check, Search, X } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { Button, Card } from "../common/Ui";
import { useData } from "../../contexts/DataContext";
import { useToast } from "../../contexts/ToastContext";
import { api } from "../../services/api";
import { formatPrice } from "../../utils/format";

const maxSelected = 15;

function priceTone(value, stats) {
  if (!Number.isFinite(value) || !stats) return "bg-slate-100 text-slate-500 dark:bg-slate-900 dark:text-slate-400";
  if (stats.min === stats.max) return "bg-sky-50 text-sky-700 ring-1 ring-sky-200 dark:bg-sky-500/10 dark:text-sky-200 dark:ring-sky-500/20";
  if (value === stats.min) return "bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200 dark:bg-emerald-500/10 dark:text-emerald-200 dark:ring-emerald-500/20";
  if (value === stats.max) return "bg-rose-50 text-rose-700 ring-1 ring-rose-200 dark:bg-rose-500/10 dark:text-rose-200 dark:ring-rose-500/20";
  return "bg-amber-50 text-amber-700 ring-1 ring-amber-200 dark:bg-amber-500/10 dark:text-amber-200 dark:ring-amber-500/20";
}

export default function StationComparison() {
  const { stations, fuels } = useData();
  const { showToast } = useToast();
  const [selected, setSelected] = useState([]);
  const [query, setQuery] = useState("");
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState(null);
  const [listDragging, setListDragging] = useState(false);
  const stationListRef = useRef(null);
  const listDragState = useRef(null);
  const suppressListClick = useRef(false);

  const stationOptions = useMemo(() => [...stations].sort((a, b) => a.name.localeCompare(b.name, "uk")), [stations]);
  const selectedSet = useMemo(() => new Set(selected.map(Number)), [selected]);
  const selectedStations = useMemo(() => stationOptions.filter(item => selectedSet.has(Number(item.id))), [stationOptions, selectedSet]);
  const visibleStations = useMemo(() => {
    const lower = query.trim().toLowerCase();
    if (!lower) return stationOptions;
    return stationOptions.filter(station => `${station.name} ${station.address} ${station.city}`.toLowerCase().includes(lower));
  }, [query, stationOptions]);

  useEffect(() => {
    const list = stationListRef.current;
    if (!list) return undefined;

    const onWheel = event => {
      const delta = Math.abs(event.deltaY) >= Math.abs(event.deltaX) ? event.deltaY : event.deltaX;
      if (!delta) return;

      const atTop = list.scrollTop <= 0;
      const atBottom = list.scrollTop + list.clientHeight >= list.scrollHeight - 1;
      event.preventDefault();
      event.stopPropagation();

      const shouldScrollPage = (delta < 0 && atTop) || (delta > 0 && atBottom);
      if (shouldScrollPage) {
        if (window.__liveFuelMapLenis) {
          window.__liveFuelMapLenis.scrollTo(window.scrollY + delta, { duration: 0.35 });
        } else {
          window.scrollBy({ top: delta, behavior: "auto" });
        }
        return;
      }

      list.scrollTop += delta;
    };

    list.addEventListener("wheel", onWheel, { passive: false });
    return () => list.removeEventListener("wheel", onWheel);
  }, []);

  const priceStats = useMemo(() => {
    if (!result?.stations?.length) return {};
    return fuels.reduce((acc, fuel) => {
      const values = result.stations
        .map(station => Number(station.prices?.[fuel.id]?.price))
        .filter(Number.isFinite);
      if (values.length) acc[fuel.id] = { min: Math.min(...values), max: Math.max(...values) };
      return acc;
    }, {});
  }, [fuels, result]);

  function toggle(id) {
    const stationId = Number(id);
    setSelected(current => {
      if (current.includes(stationId)) return current.filter(item => item !== stationId);
      if (current.length >= maxSelected) {
        showToast("Ліміт порівняння", `Можна обрати до ${maxSelected} АЗС одночасно.`, "warning");
        return current;
      }
      return [...current, stationId];
    });
  }

  function selectVisible() {
    setSelected(current => {
      const next = current.map(Number);
      let limited = false;
      for (const station of visibleStations) {
        const stationId = Number(station.id);
        if (next.includes(stationId)) continue;
        if (next.length >= maxSelected) {
          limited = true;
          break;
        }
        next.push(stationId);
      }
      if (limited) {
        showToast("Ліміт порівняння", `Додано максимум ${maxSelected} АЗС. Звузьте пошук, щоб обрати інші.`, "warning");
      }
      return next;
    });
  }

  function selectAll() {
    const next = visibleStations.slice(0, maxSelected).map(station => Number(station.id));
    setSelected(next);
    if (visibleStations.length > maxSelected) {
      showToast("Ліміт порівняння", `Обрано перші ${maxSelected} АЗС із ${visibleStations.length}.`, "warning");
    }
  }

  function startListDrag(event) {
    if (event.pointerType === "mouse" && event.button !== 0) return;
    if (event.target.closest("input,button,a,select,textarea")) return;

    const list = stationListRef.current;
    if (!list) return;

    listDragState.current = {
      pointerId: event.pointerId,
      x: event.clientX,
      y: event.clientY,
      scrollTop: list.scrollTop,
      moved: false,
      captured: false
    };
  }

  function moveListDrag(event) {
    const state = listDragState.current;
    const list = stationListRef.current;
    if (!state || !list) return;

    const deltaX = event.clientX - state.x;
    const deltaY = event.clientY - state.y;
    if (Math.abs(deltaY) > 6 && Math.abs(deltaY) > Math.abs(deltaX) * 0.8) {
      state.moved = true;
      if (!state.captured) {
        list.setPointerCapture?.(state.pointerId);
        state.captured = true;
      }
      setListDragging(true);
    }

    if (state.moved) {
      event.preventDefault();
      list.scrollTop = state.scrollTop - deltaY;
    }
  }

  function finishListDrag() {
    const state = listDragState.current;
    const list = stationListRef.current;
    if (!state || !list) return;

    try {
      if (state.captured) list.releasePointerCapture?.(state.pointerId);
    } catch {
      // Pointer capture can be released by the browser before pointerup.
    }

    if (state.moved) {
      suppressListClick.current = true;
      window.setTimeout(() => {
        suppressListClick.current = false;
      }, 120);
    }

    listDragState.current = null;
    setListDragging(false);
  }

  async function compare() {
    if (selected.length < 2) return;
    setLoading(true);
    try {
      setResult(await api.compare(selected.map(Number)));
    } catch (error) {
      showToast("Порівняння недоступне", error.message, "danger");
    } finally {
      setLoading(false);
    }
  }

  return (
    <Card className="grid select-none gap-5">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h3 className="text-lg font-black">Порівняння АЗС</h3>
          <p className="text-sm text-slate-500">Оберіть 2-{maxSelected} станцій, порівняйте ціни й швидко побачте найдешевші та найдорожчі позиції.</p>
        </div>
        <Button type="button" onClick={compare} loading={loading} disabled={selected.length < 2}>Порівняти</Button>
      </div>

      <div className="grid gap-4 lg:grid-cols-[minmax(280px,360px)_1fr]">
        <div className="rounded-2xl border border-slate-200 bg-white/65 p-3 dark:border-slate-800 dark:bg-slate-950/35">
          <label className="relative block">
            <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
            <input className="field pl-9" value={query} onChange={event => setQuery(event.target.value)} placeholder="Пошук АЗС" />
          </label>
          <div className="mt-3 flex flex-wrap gap-2">
            <Button type="button" variant="secondary" onClick={selectAll}>Обрати всі</Button>
            <Button type="button" variant="secondary" onClick={selectVisible}>Додати з пошуку</Button>
            <Button type="button" variant="ghost" onClick={() => setSelected([])}>Очистити</Button>
          </div>
          <div
            ref={stationListRef}
            data-lenis-prevent-wheel
            onPointerDown={startListDrag}
            onPointerMove={moveListDrag}
            onPointerUp={finishListDrag}
            onPointerCancel={finishListDrag}
            onClickCapture={event => {
              if (suppressListClick.current) {
                event.preventDefault();
                event.stopPropagation();
              }
            }}
            className={`mt-3 max-h-80 space-y-2 overflow-y-auto pr-1 ${listDragging ? "cursor-grabbing" : "cursor-grab"}`}
          >
            {visibleStations.map(station => {
              const checked = selectedSet.has(Number(station.id));
              return (
                <label
                  key={station.id}
                  className={`flex cursor-pointer items-start gap-3 rounded-xl border p-3 text-sm transition hover:-translate-y-0.5 ${
                    checked
                      ? "border-brand-300 bg-brand-50 text-brand-900 dark:border-brand-500/40 dark:bg-brand-500/10 dark:text-brand-50"
                      : "border-slate-200 bg-slate-50 dark:border-slate-800 dark:bg-slate-900"
                  }`}
                >
                  <input type="checkbox" checked={checked} onChange={() => toggle(station.id)} className="mt-1" />
                  <span>
                    <strong className="block">{station.name}</strong>
                    <span className="block text-xs text-slate-500">{station.address || station.city}</span>
                  </span>
                </label>
              );
            })}
          </div>
        </div>

        <div className="grid content-start gap-4">
          <div className="rounded-2xl border border-slate-200 bg-slate-50/80 p-3 dark:border-slate-800 dark:bg-slate-900/60">
            <div className="mb-2 flex items-center justify-between gap-3">
              <span className="text-sm font-bold">Обрано: {selected.length}/{maxSelected}</span>
              {selected.length >= 2 && <span className="text-xs text-slate-500">Готово до порівняння</span>}
            </div>
            <div className="flex flex-wrap gap-2">
              {selectedStations.length === 0 && <span className="text-sm text-slate-500">Позначте АЗС у списку ліворуч.</span>}
              {selectedStations.map(station => (
                <button key={station.id} type="button" className="badge-soft transition hover:-translate-y-0.5" onClick={() => toggle(station.id)}>
                  <Check className="h-3.5 w-3.5" /> {station.name} <X className="h-3.5 w-3.5" />
                </button>
              ))}
            </div>
          </div>

          {result && (
            <div className="table-wrap overflow-x-auto">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>АЗС</th>
                    {fuels.map(fuel => <th key={fuel.id}>{fuel.name}</th>)}
                  </tr>
                </thead>
                <tbody>
                  {result.stations.map(station => (
                    <tr key={station.id}>
                      <td><strong>{station.name}</strong><div className="text-xs text-slate-500">{station.address}</div></td>
                      {fuels.map(fuel => {
                        const value = Number(station.prices?.[fuel.id]?.price);
                        return (
                          <td key={fuel.id}>
                            <span className={`inline-flex min-w-24 justify-center rounded-full px-3 py-1 text-xs font-black ${priceTone(value, priceStats[fuel.id])}`}>
                              {Number.isFinite(value) ? `${formatPrice(value)} грн` : "-"}
                            </span>
                          </td>
                        );
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
              <div className="mt-3 flex flex-wrap gap-2 text-xs font-semibold">
                <span className="rounded-full bg-emerald-50 px-3 py-1 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-200">найнижча ціна</span>
                <span className="rounded-full bg-amber-50 px-3 py-1 text-amber-700 dark:bg-amber-500/10 dark:text-amber-200">середній рівень</span>
                <span className="rounded-full bg-rose-50 px-3 py-1 text-rose-700 dark:bg-rose-500/10 dark:text-rose-200">найвища ціна</span>
              </div>
            </div>
          )}
        </div>
      </div>
    </Card>
  );
}
