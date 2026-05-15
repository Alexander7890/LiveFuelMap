import { ChevronLeft, ChevronRight, Search } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import StationCard from "./StationCard";
import { Button, Select } from "../common/Ui";
import { useData } from "../../contexts/DataContext";
import { Stagger, StaggerItem } from "../../motion/MotionPrimitives.jsx";

function stationRating(station) {
  const rating = Number(station.averageRating);
  return Number.isFinite(rating) && Number(station.reviewCount) > 0 ? rating : null;
}

function stationPrices(station) {
  return (station.prices || []).filter(item => Number.isFinite(Number(item.price)));
}

function benchmarkPrice(station, fuelFilter) {
  const prices = stationPrices(station);
  if (fuelFilter !== "all") return prices.find(item => item.fuelCode === fuelFilter) || null;
  for (const code of ["a95", "a95plus", "a92", "diesel", "gas"]) {
    const price = prices.find(item => item.fuelCode === code);
    if (price) return price;
  }
  return prices[0] || null;
}

export default function StationCarousel({ activeFuel, setActiveFuel, onShowComments }) {
  const { stations, fuels, comments } = useData();
  const [query, setQuery] = useState("");
  const [ratingFilter, setRatingFilter] = useState("all");
  const [sort, setSort] = useState("name");
  const [paused, setPaused] = useState(false);
  const [dragging, setDragging] = useState(false);
  const viewportRef = useRef(null);
  const dragState = useRef(null);
  const hoverState = useRef(false);
  const suppressClick = useRef(false);
  const resumeAutoplay = useRef(null);

  const filtered = useMemo(() => {
    const lower = query.trim().toLowerCase();
    return [...stations]
      .filter(station => {
        const text = !lower || `${station.name} ${station.address} ${station.city}`.toLowerCase().includes(lower);
        const fuel = activeFuel === "all" || station.prices?.some(price => price.fuelCode === activeFuel);
        const rating = stationRating(station);
        const ratingOk =
          ratingFilter === "all" ||
          (ratingFilter === "rated" && rating !== null) ||
          (ratingFilter === "unrated" && rating === null) ||
          (ratingFilter === "4plus" && rating >= 4) ||
          (ratingFilter === "below3" && rating !== null && rating < 3);
        return text && fuel && ratingOk;
      })
      .sort((a, b) => {
        if (sort === "price-asc" || sort === "price-desc") {
          const left = benchmarkPrice(a, activeFuel)?.price;
          const right = benchmarkPrice(b, activeFuel)?.price;
          if (left == null && right == null) return a.name.localeCompare(b.name, "uk");
          if (left == null) return 1;
          if (right == null) return -1;
          return sort === "price-asc" ? left - right : right - left;
        }
        if (sort === "rating-desc") return (stationRating(b) || 0) - (stationRating(a) || 0);
        return a.name.localeCompare(b.name, "uk");
      });
  }, [stations, query, activeFuel, ratingFilter, sort]);

  useEffect(() => {
    if (paused || filtered.length < 2) return undefined;
    const timer = window.setInterval(() => scrollByCards(3), 10000);
    return () => window.clearInterval(timer);
  }, [paused, filtered.length]);

  useEffect(() => () => {
    if (resumeAutoplay.current) window.clearTimeout(resumeAutoplay.current);
  }, []);

  function scrollByCards(count) {
    const viewport = viewportRef.current;
    if (!viewport) return;
    const card = viewport.querySelector("article");
    const track = viewport.querySelector(".station-card-track");
    const gap = Number.parseFloat(window.getComputedStyle(track || viewport).columnGap || "16");
    const step = card ? (card.getBoundingClientRect().width + gap) * count : 1040;
    const atEnd = viewport.scrollLeft + viewport.clientWidth >= viewport.scrollWidth - 8;
    const nextLeft = count > 0 && atEnd ? 0 : Math.max(0, viewport.scrollLeft + step);
    viewport.scrollTo({ left: nextLeft, behavior: "smooth" });
  }

  function latestComment(stationId) {
    return [...comments].filter(item => Number(item.stationId) === Number(stationId)).sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt))[0];
  }

  function clearAutoplayResume() {
    if (resumeAutoplay.current) {
      window.clearTimeout(resumeAutoplay.current);
      resumeAutoplay.current = null;
    }
  }

  function handleRailEnter() {
    hoverState.current = true;
    clearAutoplayResume();
    setPaused(true);
  }

  function handleRailLeave() {
    hoverState.current = false;
    if (!dragState.current) setPaused(false);
  }

  function startDrag(event) {
    if (event.pointerType === "mouse" && event.button !== 0) return;
    if (event.target.closest("button,a,input,select,textarea,label")) return;

    const viewport = viewportRef.current;
    if (!viewport) return;

    dragState.current = {
      pointerId: event.pointerId,
      x: event.clientX,
      y: event.clientY,
      scrollLeft: viewport.scrollLeft,
      moved: false,
      captured: false
    };
    setPaused(true);
    clearAutoplayResume();
  }

  function moveDrag(event) {
    const state = dragState.current;
    const viewport = viewportRef.current;
    if (!state || !viewport) return;

    const deltaX = event.clientX - state.x;
    const deltaY = event.clientY - state.y;
    if (Math.abs(deltaX) > 8 && Math.abs(deltaX) > Math.abs(deltaY) * 1.15) {
      state.moved = true;
      if (!state.captured) {
        viewport.setPointerCapture?.(state.pointerId);
        state.captured = true;
      }
      setDragging(true);
    }

    if (state.moved) {
      event.preventDefault();
      viewport.scrollLeft = state.scrollLeft - deltaX;
    }
  }

  function finishDrag(event) {
    const state = dragState.current;
    const viewport = viewportRef.current;
    if (!state || !viewport) return;

    try {
      if (state.captured) viewport.releasePointerCapture?.(state.pointerId);
    } catch {
      // Pointer capture may already be released if the drag was cancelled by the browser.
    }
    if (state.moved) {
      suppressClick.current = true;
      window.setTimeout(() => {
        suppressClick.current = false;
      }, 120);
    }
    dragState.current = null;
    setDragging(false);
    clearAutoplayResume();
    resumeAutoplay.current = window.setTimeout(() => {
      if (!hoverState.current) setPaused(false);
      resumeAutoplay.current = null;
    }, 1600);
  }

  return (
    <div className="panel premium-card p-4" onMouseEnter={handleRailEnter} onMouseLeave={handleRailLeave}>
      <Stagger className="grid gap-3 lg:grid-cols-[1fr_180px_180px_180px]">
        <StaggerItem as="label" className="relative">
          <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
          <input className="field pl-9" value={query} onChange={event => setQuery(event.target.value)} placeholder="Пошук за назвою, адресою, містом" />
        </StaggerItem>
        <StaggerItem>
          <Select value={activeFuel} onChange={event => setActiveFuel(event.target.value)}>
            <option value="all">Усі типи пального</option>
            {fuels.map(fuel => <option key={fuel.code} value={fuel.code}>{fuel.name}</option>)}
          </Select>
        </StaggerItem>
        <StaggerItem>
          <Select value={ratingFilter} onChange={event => setRatingFilter(event.target.value)}>
            <option value="all">Будь-який рейтинг</option>
            <option value="rated">З рейтингом</option>
            <option value="unrated">Без рейтингу</option>
            <option value="4plus">4+ зірки</option>
            <option value="below3">Нижче 3</option>
          </Select>
        </StaggerItem>
        <StaggerItem>
          <Select value={sort} onChange={event => setSort(event.target.value)}>
            <option value="name">За назвою</option>
            <option value="price-asc">Від дешевих</option>
            <option value="price-desc">Від дорогих</option>
            <option value="rating-desc">За рейтингом</option>
          </Select>
        </StaggerItem>
      </Stagger>
      <div className="mt-4 flex items-center justify-between gap-3">
        <div className="text-sm font-medium text-slate-600 dark:text-slate-300">Знайдено: {filtered.length}</div>
        <div className="flex gap-2">
          <Button variant="secondary" onClick={() => scrollByCards(-3)}><ChevronLeft className="h-4 w-4" /></Button>
          <Button variant="secondary" onClick={() => scrollByCards(3)}><ChevronRight className="h-4 w-4" /></Button>
        </div>
      </div>
      <div
        ref={viewportRef}
        onPointerDown={startDrag}
        onPointerMove={moveDrag}
        onPointerUp={finishDrag}
        onPointerCancel={finishDrag}
        onClickCapture={event => {
          if (suppressClick.current) {
            event.preventDefault();
            event.stopPropagation();
          }
        }}
        className={`station-card-rail mt-4 overflow-x-auto scroll-smooth ${dragging ? "cursor-grabbing select-none" : "cursor-grab"}`}
      >
        <div className="station-card-track flex gap-4">
          {filtered.map(station => (
            <div key={station.id} className="shrink-0">
              <StationCard station={station} fuelFilter={activeFuel} latestComment={latestComment(station.id)} onShowComments={onShowComments} />
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
