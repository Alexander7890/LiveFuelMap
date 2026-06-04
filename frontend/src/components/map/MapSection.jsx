import { GoogleMap } from "@capacitor/google-maps";
import { Capacitor } from "@capacitor/core";
import { ExternalLink, Loader2, MapPin, Navigation, X } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { Card, Select } from "../common/Ui";
import { useData } from "../../contexts/DataContext";
import { formatPrice } from "../../utils/format";
import { GOOGLE_MAPS_API_KEY } from "../../config";
import {
  buildNetworkPriceSummaries,
  createDirectionsUrl,
  KHARKIV_CENTER,
  searchGoogleFuelStations
} from "../../services/googlePlaces";
import { useTranslation } from "react-i18next";

const DEFAULT_CENTER = KHARKIV_CENTER;
const LABEL_ZOOM_THRESHOLD = 14;
const RED_MARKER_TINT = { r: 220, g: 38, b: 38, a: 1 };
const PLAIN_RED_MARKER_ICON = {
  url: `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(`
<svg xmlns="http://www.w3.org/2000/svg" width="38" height="54" viewBox="0 0 38 54">
  <path fill="#dc2626" stroke="#991b1b" stroke-width="2" d="M19 52s17-18.5 17-33A17 17 0 1 0 2 19c0 14.5 17 33 17 33z"/>
  <circle cx="19" cy="19" r="7" fill="#fff"/>
</svg>
`)}`,
  width: 38,
  height: 54
};

function createMapInstanceId() {
  const randomPart = globalThis.crypto?.randomUUID?.() || `${Date.now()}-${Math.random().toString(36).slice(2)}`;
  return `livefuelmap-stations-map-${randomPart}`;
}

function getCheapestPrice(station) {
  return [...(station?.prices || [])].sort((a, b) => Number(a.price) - Number(b.price))[0];
}

function getStationSnippet(station, t) {
  const cheapest = getCheapestPrice(station);
  const priceLine = cheapest
    ? `${cheapest.fuelName}: ${formatPrice(cheapest.price)} ${t("common.currencyShort")}`
    : text(t, "map.priceUnavailable", "Ціна наразі відсутня");

  return [station.address, priceLine].filter(Boolean).join("\n");
}

function getBounds(coordinates) {
  const lats = coordinates.map(point => point.lat);
  const lngs = coordinates.map(point => point.lng);
  const southwest = { lat: Math.min(...lats), lng: Math.min(...lngs) };
  const northeast = { lat: Math.max(...lats), lng: Math.max(...lngs) };

  return {
    southwest,
    center: {
      lat: (southwest.lat + northeast.lat) / 2,
      lng: (southwest.lng + northeast.lng) / 2
    },
    northeast
  };
}

function text(t, key, defaultValue, values = {}) {
  return t(key, { defaultValue, ...values });
}

function getMarkerTitle(station) {
  const displayName = station.displayName || station.networkName;
  if (!station.networkName || station.networkName === displayName) return displayName;
  return `${station.networkName} - ${displayName}`;
}

function getMarkerLabel(station) {
  return String(station.displayName || station.networkName || "").replace(/\s+/g, " ").trim();
}

function truncateLabel(value) {
  return value.length > 26 ? `${value.slice(0, 23).trim()}...` : value;
}

function escapeSvgText(value) {
  return value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function createLabeledMarkerIconUrl(label) {
  const safeLabel = escapeSvgText(truncateLabel(label));
  const width = Math.max(86, Math.min(236, Math.round(safeLabel.length * 8 + 34)));
  const pinX = Math.round(width / 2 - 19);
  const svg = `
<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="78" viewBox="0 0 ${width} 78">
  <defs>
    <filter id="labelShadow" x="-20%" y="-40%" width="140%" height="180%">
      <feDropShadow dx="0" dy="2" stdDeviation="2" flood-color="#0f172a" flood-opacity="0.22"/>
    </filter>
  </defs>
  <rect x="4" y="2" width="${width - 8}" height="28" rx="8" fill="#ffffff" stroke="#cbd5e1" filter="url(#labelShadow)"/>
  <text x="${width / 2}" y="21" text-anchor="middle" font-family="Arial, sans-serif" font-size="13" font-weight="700" fill="#0f172a">${safeLabel}</text>
  <g transform="translate(${pinX} 24)">
    <path fill="#dc2626" stroke="#991b1b" stroke-width="2" d="M19 52s17-18.5 17-33A17 17 0 1 0 2 19c0 14.5 17 33 17 33z"/>
    <circle cx="19" cy="19" r="7" fill="#fff"/>
  </g>
</svg>`;

  return {
    url: `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`,
    width,
    height: 78
  };
}

function buildMarker(station, t, showLabel) {
  const marker = {
    coordinate: { lat: station.latitude, lng: station.longitude },
    title: getMarkerTitle(station),
    snippet: getStationSnippet(station, t),
    tintColor: RED_MARKER_TINT
  };

  if (Capacitor.getPlatform() === "web") {
    const icon = showLabel
      ? createLabeledMarkerIconUrl(getMarkerLabel(station))
      : PLAIN_RED_MARKER_ICON;
    marker.iconUrl = icon.url;
    marker.iconSize = { width: icon.width, height: icon.height };
  }

  return marker;
}

export default function MapSection() {
  const { t, i18n } = useTranslation();
  const { stations } = useData();
  const [network, setNetwork] = useState("all");
  const [mapReady, setMapReady] = useState(false);
  const [mapError, setMapError] = useState("");
  const [placesLoading, setPlacesLoading] = useState(false);
  const [placesError, setPlacesError] = useState("");
  const [googleStations, setGoogleStations] = useState([]);
  const [selectedStationId, setSelectedStationId] = useState(null);
  const [mapZoom, setMapZoom] = useState(11);
  const mapElementRef = useRef(null);
  const mapInstanceRef = useRef(null);
  const markerIdsRef = useRef([]);
  const markerStationsRef = useRef(new Map());

  useEffect(() => {
    if (Capacitor.getPlatform() !== "android") return undefined;

    document.documentElement.classList.add("livefuelmap-android-native-map");
    return () => document.documentElement.classList.remove("livefuelmap-android-native-map");
  }, []);

  const networks = useMemo(
    () => buildNetworkPriceSummaries(stations).sort((a, b) => a.name.localeCompare(b.name, i18n.language || "uk")),
    [stations, i18n.language]
  );

  const selectedNetworks = useMemo(() => {
    if (network === "all") return networks;
    return networks.filter(item => item.key === network);
  }, [network, networks]);

  const selectedStation = useMemo(
    () => googleStations.find(station => station.id === selectedStationId) || null,
    [selectedStationId, googleStations]
  );
  const markerLabelsVisible = network !== "all" || mapZoom >= LABEL_ZOOM_THRESHOLD;

  useEffect(() => {
    if (network !== "all" && !networks.some(item => item.key === network)) {
      setNetwork("all");
    }
  }, [network, networks]);

  useEffect(() => {
    let cancelled = false;
    let createTimerId;

    async function createMap() {
      if (!GOOGLE_MAPS_API_KEY || !mapElementRef.current) {
        setMapReady(false);
        return;
      }

      const mapId = createMapInstanceId();
      setMapError("");
      setMapReady(false);

      try {
        const map = await GoogleMap.create({
          id: mapId,
          element: mapElementRef.current,
          apiKey: GOOGLE_MAPS_API_KEY,
          region: "UA",
          language: (i18n.language || "uk").split("-")[0],
          config: {
            center: DEFAULT_CENTER,
            zoom: 11,
            mapTypeControl: false,
            streetViewControl: false,
            fullscreenControl: true,
            clickableIcons: false,
            androidLiteMode: false
          }
        });

        if (cancelled) {
          await map.destroy().catch(() => {});
          return;
        }

        mapInstanceRef.current = map;
        await map.setOnMarkerClickListener(event => {
          const stationId = markerStationsRef.current.get(event.markerId);
          if (stationId) setSelectedStationId(stationId);
        });
        await map.setOnMapClickListener(() => setSelectedStationId(null));
        await map.setOnCameraIdleListener(event => {
          const nextZoom = Number(event?.zoom);
          if (Number.isFinite(nextZoom)) {
            setMapZoom(current => (Math.abs(current - nextZoom) >= 0.1 ? nextZoom : current));
          }
        });
        setMapReady(true);
      } catch (error) {
        if (!cancelled) {
          console.error("Google Maps initialization failed", error);
          setMapError(text(t, "map.googleUnavailable", "Google Maps тимчасово недоступний."));
          setMapReady(false);
        }
      }
    }

    createTimerId = window.setTimeout(createMap, 0);

    return () => {
      cancelled = true;
      if (createTimerId) window.clearTimeout(createTimerId);
      const map = mapInstanceRef.current;
      mapInstanceRef.current = null;
      markerIdsRef.current = [];
      markerStationsRef.current = new Map();
      if (map) map.destroy().catch(() => {});
    };
  }, [i18n.language, t]);

  useEffect(() => {
    let cancelled = false;

    setSelectedStationId(null);
    setGoogleStations([]);
    setPlacesError("");

    if (!GOOGLE_MAPS_API_KEY || !mapReady || selectedNetworks.length === 0) {
      setPlacesLoading(false);
      return undefined;
    }

    async function loadGoogleStations() {
      setPlacesLoading(true);

      try {
        const results = await searchGoogleFuelStations(selectedNetworks, i18n.language);
        if (!cancelled) {
          setGoogleStations(results);
        }
      } catch (error) {
        if (!cancelled) {
          console.error("Google Places search failed", error);
          setPlacesError(text(t, "map.googlePlacesUnavailable", "Google Places тимчасово недоступний."));
        }
      } finally {
        if (!cancelled) setPlacesLoading(false);
      }
    }

    loadGoogleStations();

    return () => {
      cancelled = true;
    };
  }, [mapReady, selectedNetworks, i18n.language, t]);

  useEffect(() => {
    if (!mapReady || !mapInstanceRef.current) return undefined;

    let cancelled = false;
    const map = mapInstanceRef.current;

    async function syncMarkers() {
      const oldMarkerIds = markerIdsRef.current;
      if (oldMarkerIds.length) {
        await map.removeMarkers(oldMarkerIds).catch(() => {});
      }

      markerIdsRef.current = [];
      markerStationsRef.current = new Map();

      const markerItems = googleStations.map(station => ({
        station,
        marker: buildMarker(station, t, markerLabelsVisible)
      }));

      if (!markerItems.length) return;

      const markerIds = await map.addMarkers(markerItems.map(item => item.marker));
      if (cancelled) {
        await map.removeMarkers(markerIds).catch(() => {});
        return;
      }

      markerIdsRef.current = markerIds;
      markerStationsRef.current = new Map(markerIds.map((id, index) => [id, markerItems[index].station.id]));

    }

    syncMarkers().catch(error => {
      console.error("Google Maps marker sync failed", error);
      if (!cancelled) setMapError(text(t, "map.googleMarkersUnavailable", "Не вдалося показати маркери Google Maps."));
    });

    return () => {
      cancelled = true;
    };
  }, [googleStations, mapReady, markerLabelsVisible, t]);

  useEffect(() => {
    if (!mapReady || !mapInstanceRef.current || googleStations.length === 0) return undefined;

    let cancelled = false;
    const map = mapInstanceRef.current;

    async function fitCurrentStations() {
      const coordinates = googleStations.map(station => ({ lat: station.latitude, lng: station.longitude }));
      if (coordinates.length === 1) {
        await map.setCamera({ coordinate: coordinates[0], zoom: 14, animate: true }).catch(() => {});
      } else {
        await map.fitBounds(getBounds(coordinates), network === "all" ? 72 : 96).catch(() => {});
      }

      if (!cancelled && network !== "all") {
        setMapZoom(current => Math.max(current, LABEL_ZOOM_THRESHOLD));
      }
    }

    fitCurrentStations();

    return () => {
      cancelled = true;
    };
  }, [googleStations, mapReady, network]);

  useEffect(() => {
    if (selectedStationId && !googleStations.some(station => station.id === selectedStationId)) {
      setSelectedStationId(null);
    }
  }, [selectedStationId, googleStations]);

  const noNetworksMessage = !placesLoading && selectedNetworks.length === 0
    ? text(t, "map.noPricedNetworks", "У базі поки немає мереж із доступними цінами.")
    : "";

  const noStationsMessage = !placesLoading && !placesError && mapReady && selectedNetworks.length > 0 && googleStations.length === 0
    ? network === "all"
      ? text(t, "map.noGoogleStationsAll", "Фізичні станції доступних мереж на карті не знайдено.")
      : text(t, "map.noGoogleStations", "Фізичні станції цієї мережі на карті не знайдено.")
    : "";

  const keyMissingMessage = !GOOGLE_MAPS_API_KEY
    ? text(t, "map.googleKeyMissing", "Google Maps API key не налаштований.")
    : "";

  const statusMessage = keyMissingMessage || mapError || placesError || noNetworksMessage || noStationsMessage;

  return (
    <div className="grid gap-5">
      <Card>
        <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
          <div className="min-w-0">
            <h3 className="text-lg font-black">{t("map.title")}</h3>
            {!placesLoading && googleStations.length > 0 && (
              <p className="mt-2 text-xs font-semibold text-slate-500 dark:text-slate-300">
                {text(t, "map.googleResults", "Знайдено фізичних АЗС: {{count}}", { count: googleStations.length })}
              </p>
            )}
          </div>
          <div className="w-full md:w-72">
            <Select value={network} onChange={event => setNetwork(event.target.value)}>
              <option value="all">{t("map.allOperators")}</option>
              {networks.map(item => <option key={item.key} value={item.key}>{item.name}</option>)}
            </Select>
          </div>
        </div>
      </Card>

      <div className="relative min-h-[360px] overflow-hidden rounded-3xl border border-slate-200 bg-slate-100 shadow-panel dark:border-slate-800 dark:bg-slate-950">
        {GOOGLE_MAPS_API_KEY && <capacitor-google-map ref={mapElementRef} class="livefuelmap-google-map"></capacitor-google-map>}

        {(placesLoading || (!mapReady && GOOGLE_MAPS_API_KEY && !mapError)) && (
          <div className="absolute inset-0 z-10 grid place-items-center bg-white/75 text-slate-600 backdrop-blur-sm dark:bg-slate-950/65 dark:text-slate-300">
            <span className="inline-flex items-center gap-2 text-sm font-semibold">
              <Loader2 className="h-4 w-4 animate-spin" />
              {placesLoading
                ? text(t, "map.searchingGoogle", "Шукаємо АЗС у Google Maps...")
                : t("common.loading")}
            </span>
          </div>
        )}

        {statusMessage && !placesLoading && (
          <div className="absolute inset-0 z-10 grid place-items-center bg-white/80 p-4 text-center backdrop-blur-sm dark:bg-slate-950/70">
            <div className="max-w-md rounded-2xl border border-slate-200 bg-white/95 p-4 text-sm font-semibold text-slate-700 shadow-panel dark:border-slate-800 dark:bg-slate-950/95 dark:text-slate-200">
              {statusMessage}
            </div>
          </div>
        )}

        {selectedStation && (
          <div className="absolute inset-x-3 bottom-3 z-20 rounded-2xl border border-slate-200 bg-white/95 p-4 shadow-panel backdrop-blur dark:border-slate-800 dark:bg-slate-950/95 sm:inset-x-auto sm:left-4 sm:max-w-sm">
            <div className="flex items-start gap-3">
              <span className="grid h-10 w-10 shrink-0 place-items-center rounded-2xl bg-brand-50 text-brand-700 dark:bg-brand-500/15 dark:text-brand-100">
                <MapPin className="h-5 w-5" />
              </span>
              <div className="min-w-0 flex-1">
                <div className="flex items-start gap-2">
                  <div className="min-w-0">
                    <div className="truncate font-bold">{selectedStation.networkName}</div>
                    <div className="break-words text-sm font-semibold text-slate-700 dark:text-slate-100">{selectedStation.displayName}</div>
                  </div>
                  <button
                    type="button"
                    className="ml-auto grid h-8 w-8 shrink-0 place-items-center rounded-full text-slate-500 transition hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800"
                    onClick={() => setSelectedStationId(null)}
                    aria-label={t("common.close")}
                  >
                    <X className="h-4 w-4" />
                  </button>
                </div>

                <div className="mt-1 break-words text-sm text-slate-500">{selectedStation.address}</div>

                <div className="mt-3 flex flex-wrap gap-2">
                  {selectedStation.prices.length > 0 ? selectedStation.prices.map(price => (
                    <span key={price.fuelCode || price.fuelName} className="badge-soft">
                      {price.fuelName}: {formatPrice(price.price)} {t("common.currencyShort")}
                    </span>
                  )) : (
                    <span className="badge-soft">{text(t, "map.priceUnavailable", "Ціна наразі відсутня")}</span>
                  )}
                </div>

                <div className="mt-4 flex flex-wrap gap-2">
                  <a
                    className="btn-primary min-h-10 px-3 py-2 text-sm"
                    href={createDirectionsUrl(selectedStation)}
                    target="_blank"
                    rel="noreferrer"
                  >
                    <Navigation className="h-4 w-4" />
                    {text(t, "map.route", "Побудувати маршрут")}
                  </a>
                  {selectedStation.googleMapsUri && (
                    <a
                      className="btn-secondary min-h-10 px-3 py-2 text-sm"
                      href={selectedStation.googleMapsUri}
                      target="_blank"
                      rel="noreferrer"
                    >
                      <ExternalLink className="h-4 w-4" />
                      {text(t, "map.openGoogleMaps", "Google Maps")}
                    </a>
                  )}
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
