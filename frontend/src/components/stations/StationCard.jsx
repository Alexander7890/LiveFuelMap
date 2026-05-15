import { motion } from "framer-motion";
import { ExternalLink, MessageCircle, RotateCcw } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import Stars from "../common/Stars";
import { Button } from "../common/Ui";
import { formatPrice, imageUrl, truncate } from "../../utils/format";
import { premiumEase } from "../../motion/presets.js";

const fuelOrder = ["a95plus", "a95", "a92", "diesel", "gas"];

function orderedPrices(station, fuelFilter) {
  const prices = station.prices || [];
  const filtered = fuelFilter === "all" ? prices : prices.filter(item => item.fuelCode === fuelFilter);
  return [...filtered].sort((a, b) => fuelOrder.indexOf(a.fuelCode) - fuelOrder.indexOf(b.fuelCode));
}

function backPhotos(station) {
  const fallback = "/default-station11.jpg";
  const urls = (station.photoUrls || [])
    .map(item => String(item || "").trim())
    .filter(Boolean)
    .filter(item => item !== "default-station.jpg" && item !== "default-station11.jpg");
  return urls.length ? urls : [fallback];
}

export default function StationCard({ station, fuelFilter, latestComment, onShowComments }) {
  const [flipped, setFlipped] = useState(false);
  const [photoIndex, setPhotoIndex] = useState(0);
  const prices = orderedPrices(station, fuelFilter);
  const photos = useMemo(() => backPhotos(station), [station]);
  const rating = Number(station.averageRating);
  const hasRating = Number.isFinite(rating) && Number(station.reviewCount) > 0;
  const visiblePrices = prices.slice(0, 4);
  const hiddenPriceCount = Math.max(0, prices.length - visiblePrices.length);
  const tapState = useRef({ x: 0, y: 0, time: 0, moved: false });
  const lastTap = useRef({ time: 0, x: 0, y: 0 });
  const lastCustomFlip = useRef(0);

  useEffect(() => {
    if (!flipped || photos.length < 2) return undefined;
    const timer = window.setInterval(() => {
      setPhotoIndex(current => (current + 1) % photos.length);
    }, 5200);
    return () => window.clearInterval(timer);
  }, [flipped, photos.length]);

  const isInteractiveTarget = target => target.closest("button,a,input,select,textarea,label");

  const handlePointerDown = event => {
    if (event.pointerType === "mouse" && event.button !== 0) return;
    if (isInteractiveTarget(event.target)) return;
    tapState.current = {
      x: event.clientX,
      y: event.clientY,
      time: window.performance.now(),
      moved: false
    };
  };

  const resetTap = () => {
    tapState.current = { x: 0, y: 0, time: 0, moved: false };
  };

  const handlePointerMove = event => {
    const tap = tapState.current;
    if (!tap.time) return;
    if (Math.hypot(event.clientX - tap.x, event.clientY - tap.y) > 7) {
      tap.moved = true;
    }
  };

  const handlePointerUp = event => {
    const tap = tapState.current;
    if (!tap.time || tap.moved || isInteractiveTarget(event.target)) {
      resetTap();
      return;
    }

    const now = window.performance.now();
    const distance = Math.hypot(event.clientX - lastTap.current.x, event.clientY - lastTap.current.y);
    if (now - lastTap.current.time < 330 && distance < 28) {
      event.preventDefault();
      setFlipped(value => !value);
      lastCustomFlip.current = now;
      lastTap.current = { time: 0, x: 0, y: 0 };
    } else {
      lastTap.current = { time: now, x: event.clientX, y: event.clientY };
    }
    resetTap();
  };

  const handleDoubleClick = event => {
    if (isInteractiveTarget(event.target)) return;
    if (window.performance.now() - lastCustomFlip.current < 120) return;
    setFlipped(value => !value);
  };

  return (
    <motion.article
      initial={{ opacity: 0, scale: 0.985 }}
      animate={{ opacity: 1, scale: 1 }}
      whileHover={{ y: -4, scale: 1.008 }}
      transition={{ duration: 0.42, ease: premiumEase }}
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={handlePointerUp}
      onPointerCancel={resetTap}
      onDoubleClick={handleDoubleClick}
      onKeyDown={event => {
        if (event.key === "Enter" || event.key === " ") {
          event.preventDefault();
          setFlipped(value => !value);
        }
      }}
      tabIndex={0}
      className="station-card group relative h-[540px] min-w-[min(330px,calc(100vw-58px))] cursor-pointer select-none overflow-visible transform-gpu [contain:layout] [perspective:1700px]"
      title="Подвійний клік перевертає картку"
    >
      <motion.div
        className="card-flip-stage relative h-full transform-gpu"
        animate={{ rotateY: flipped ? 180 : 0 }}
        transition={{ duration: 0.72, ease: premiumEase }}
        style={{ transformStyle: "preserve-3d" }}
      >
        <div className="panel premium-card card-face card-face-front flex flex-col overflow-hidden">
          <div className="relative h-52 shrink-0 overflow-hidden">
            <motion.img
              src={imageUrl(station.imageUrl)}
              alt={station.name}
              draggable={false}
              className="h-full w-full object-cover transition duration-700 group-hover:scale-105"
              onError={event => { event.currentTarget.src = "/default-station11.jpg"; }}
            />
            <span className="pointer-events-none absolute inset-0 bg-gradient-to-t from-slate-950/45 via-transparent to-white/10" />
          </div>
          <div className="flex flex-1 flex-col p-4">
            <div className="flex items-start justify-between gap-3">
              <div>
                <h3 className="text-lg font-black">{station.name}</h3>
                <p className="mt-1 text-sm text-slate-500">{station.address || station.city}</p>
              </div>
              <span className="badge-soft">{station.city}</span>
            </div>
            <div className="station-card-prices mt-4 grid content-start gap-2">
              {prices.length === 0 && <div className="rounded-xl bg-slate-50 p-3 text-sm text-slate-500 dark:bg-slate-900">Ціни відсутні</div>}
              {visiblePrices.map(price => (
                <div
                  key={`${price.fuelId}-${price.date}`}
                  className="station-price-row flex items-center justify-between rounded-xl bg-slate-50/90 px-3 py-2 text-sm dark:bg-slate-900/90"
                >
                  <span className="font-semibold">{price.fuelName}</span>
                  <strong>{formatPrice(price.price)} грн</strong>
                </div>
              ))}
              {hiddenPriceCount > 0 && <div className="rounded-xl bg-slate-100/80 px-3 py-2 text-xs font-semibold text-slate-500 dark:bg-slate-900/80">Ще {hiddenPriceCount} позицій на звороті</div>}
            </div>
            <div className="mt-auto flex items-center justify-between border-t border-slate-200 pt-3 dark:border-slate-800">
              <div className="flex items-center gap-2 text-amber-400">
                <Stars rating={hasRating ? Math.round(rating) : 0} />
                <span className="text-sm font-semibold text-slate-600 dark:text-slate-300">{hasRating ? rating.toFixed(1) : "—"}</span>
              </div>
              <span className="text-xs text-slate-500">{station.reviewCount || 0} відгуків</span>
            </div>
          </div>
        </div>

        <div className="panel premium-card card-face card-face-back flex flex-col gap-3 overflow-hidden p-4">
          <div>
            <div className="flex items-start justify-between gap-3">
              <h3 className="text-lg font-black">{station.name}</h3>
              <RotateCcw className="h-5 w-5 text-brand-600" />
            </div>
            <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-slate-300">
              {station.city}, {station.address || "адреса не вказана"}. Координати: {Number(station.latitude || 0).toFixed(5)}, {Number(station.longitude || 0).toFixed(5)}.
            </p>
          </div>
          <div className="grid grid-cols-2 gap-2 text-sm">
            <div className="soft-panel p-3"><span className="block text-slate-500">Типів пального</span><strong>{prices.length || "—"}</strong></div>
            <div className="soft-panel p-3"><span className="block text-slate-500">Фото</span><strong>{photos[0] === "/default-station11.jpg" ? "—" : photos.length}</strong></div>
          </div>
          <div className="soft-panel p-3">
            <div className="mb-1 flex items-center justify-between">
              <span className="text-xs font-semibold uppercase text-slate-500">Останній відгук</span>
              <MessageCircle className="h-4 w-4 text-brand-600" />
            </div>
            {latestComment ? (
              <p className="text-sm leading-6">{truncate(latestComment.content, 125)}</p>
            ) : (
              <p className="text-sm text-slate-500">Відгуків поки немає.</p>
            )}
          </div>
          <div className="relative min-h-32 overflow-hidden rounded-2xl border border-slate-200 bg-slate-100 dark:border-slate-800 dark:bg-slate-900">
            <motion.img
              key={photoIndex}
              initial={{ opacity: 0, scale: 1.04 }}
              animate={{ opacity: 1, scale: 1 }}
              transition={{ duration: 0.8, ease: premiumEase }}
              src={imageUrl(photos[photoIndex])}
              alt={`${station.name} фото ${photoIndex + 1}`}
              className="h-32 w-full object-cover"
              onError={event => { event.currentTarget.src = "/default-station11.jpg"; }}
            />
            {photos.length > 1 && <span className="absolute bottom-2 right-2 rounded-full bg-slate-950/70 px-2 py-1 text-xs font-semibold text-white">{photoIndex + 1}/{photos.length}</span>}
          </div>
          <div className="mt-auto grid grid-cols-2 gap-2">
            <Button variant="secondary" onClick={() => onShowComments(station.id)}>Відгуки</Button>
            {station.websiteUrl ? (
              <a className="btn-primary" href={station.websiteUrl} target="_blank" rel="noreferrer"><ExternalLink className="h-4 w-4" /> Сайт</a>
            ) : (
              <Button variant="secondary" disabled>Сайт</Button>
            )}
          </div>
        </div>
      </motion.div>
    </motion.article>
  );
}
