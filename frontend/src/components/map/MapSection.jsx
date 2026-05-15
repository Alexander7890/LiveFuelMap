import { MapPin } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { Card, Select } from "../common/Ui";
import { useData } from "../../contexts/DataContext";
import { formatPrice } from "../../utils/format";
import { GOOGLE_MAPS_API_KEY } from "../../config";

export default function MapSection() {
  const { stations } = useData();
  const [network, setNetwork] = useState("all");
  const mapRef = useRef(null);
  const googleMapRef = useRef(null);
  const markersRef = useRef([]);

  const networks = useMemo(() => ["all", ...new Set(stations.map(item => item.name).filter(Boolean))].sort((a, b) => a.localeCompare(b, "uk")), [stations]);
  const visible = network === "all" ? stations : stations.filter(item => item.name === network);

  useEffect(() => {
    if (!GOOGLE_MAPS_API_KEY || !mapRef.current) return undefined;

    let cancelled = false;
    const scriptId = "google-maps-livefuelmap";
    const ensureScript = () => new Promise((resolve, reject) => {
      if (window.google?.maps) {
        resolve();
        return;
      }

      let script = document.getElementById(scriptId);
      if (!script) {
        script = document.createElement("script");
        script.id = scriptId;
        script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(GOOGLE_MAPS_API_KEY)}&libraries=places`;
        script.async = true;
        script.defer = true;
        script.onerror = reject;
        document.head.append(script);
      }
      script.addEventListener("load", resolve, { once: true });
      script.addEventListener("error", reject, { once: true });
    });

    ensureScript().then(() => {
      if (cancelled || !mapRef.current || !window.google?.maps) return;
      if (!googleMapRef.current) {
        googleMapRef.current = new window.google.maps.Map(mapRef.current, {
          center: { lat: 49.9935, lng: 36.2304 },
          zoom: 11,
          mapTypeControl: false,
          streetViewControl: false,
          fullscreenControl: true
        });
      }
    }).catch(() => {});

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!googleMapRef.current || !window.google?.maps) return;
    markersRef.current.forEach(marker => marker.setMap(null));
    markersRef.current = [];

    const bounds = new window.google.maps.LatLngBounds();
    visible.forEach(station => {
      const lat = Number(station.latitude);
      const lng = Number(station.longitude);
      if (!Number.isFinite(lat) || !Number.isFinite(lng) || (lat === 0 && lng === 0)) return;

      const marker = new window.google.maps.Marker({
        position: { lat, lng },
        map: googleMapRef.current,
        title: station.name
      });
      const cheapest = [...(station.prices || [])].sort((a, b) => Number(a.price) - Number(b.price))[0];
      const info = new window.google.maps.InfoWindow({
        content: `<strong>${station.name}</strong><br>${station.address || ""}<br>${cheapest ? `${cheapest.fuelName}: ${formatPrice(cheapest.price)} грн` : "Ціни відсутні"}`
      });
      marker.addListener("click", () => info.open({ anchor: marker, map: googleMapRef.current }));
      bounds.extend({ lat, lng });
      markersRef.current.push(marker);
    });

    if (markersRef.current.length) googleMapRef.current.fitBounds(bounds);
  }, [visible]);

  return (
    <Card>
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h3 className="text-lg font-black">Карта АЗС Харкова</h3>
          <p className="text-sm text-slate-500">Якщо Google Maps API key не заданий, показується інтерактивний список координат.</p>
        </div>
        <Select value={network} onChange={event => setNetwork(event.target.value)}>
          <option value="all">Усі оператори</option>
          {networks.filter(item => item !== "all").map(item => <option key={item} value={item}>{item}</option>)}
        </Select>
      </div>
      {GOOGLE_MAPS_API_KEY && <div ref={mapRef} className="mt-5 min-h-[420px] overflow-hidden rounded-3xl border border-slate-200 dark:border-slate-800" />}
      <div className="leaflet-like-map mt-5 min-h-[360px] rounded-3xl border border-slate-200 p-4 dark:border-slate-800">
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          {visible.map(station => {
            const cheapest = [...(station.prices || [])].sort((a, b) => Number(a.price) - Number(b.price))[0];
            return (
              <div key={station.id} className="rounded-2xl border border-slate-200 bg-white/90 p-4 shadow-sm backdrop-blur dark:border-slate-800 dark:bg-slate-950/90">
                <div className="flex items-start gap-3">
                  <span className="grid h-10 w-10 place-items-center rounded-2xl bg-brand-50 text-brand-700 dark:bg-brand-500/15 dark:text-brand-100"><MapPin className="h-5 w-5" /></span>
                  <div>
                    <div className="font-bold">{station.name}</div>
                    <div className="text-sm text-slate-500">{station.address}</div>
                    <div className="mt-2 text-xs text-slate-500">{Number(station.latitude).toFixed(5)}, {Number(station.longitude).toFixed(5)}</div>
                    {cheapest && <div className="mt-2 badge-soft">{cheapest.fuelName}: {formatPrice(cheapest.price)} грн</div>}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </Card>
  );
}
