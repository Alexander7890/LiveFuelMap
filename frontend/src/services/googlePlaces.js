import { GOOGLE_MAPS_API_KEY } from "../config";

const KHARKIV_BOUNDS = {
  south: 49.82,
  west: 35.98,
  north: 50.13,
  east: 36.5
};

const KHARKIV_CENTER = { lat: 49.9935, lng: 36.2304 };

const PLACE_FIELDS = [
  "id",
  "displayName",
  "formattedAddress",
  "location",
  "googleMapsURI",
  "primaryType",
  "types",
  "businessStatus"
];

const CYRILLIC_TO_LATIN = {
  а: "a",
  б: "b",
  в: "v",
  г: "h",
  ґ: "g",
  д: "d",
  е: "e",
  є: "ie",
  ж: "zh",
  з: "z",
  и: "y",
  і: "i",
  ї: "i",
  й: "i",
  к: "k",
  л: "l",
  м: "m",
  н: "n",
  о: "o",
  п: "p",
  р: "r",
  с: "s",
  т: "t",
  у: "u",
  ф: "f",
  х: "kh",
  ц: "ts",
  ч: "ch",
  ш: "sh",
  щ: "shch",
  ю: "iu",
  я: "ia",
  ь: "",
  ъ: ""
};

const NETWORK_PROFILES = [
  {
    key: "okko",
    aliases: ["OKKO", "ОККО"],
    queries: ["OKKO gas station in Kharkiv, Ukraine", "ОККО АЗС Харків"]
  },
  {
    key: "wog",
    aliases: ["WOG", "ВОГ"],
    queries: ["WOG gas station in Kharkiv, Ukraine", "WOG АЗС Харків"]
  },
  {
    key: "socar",
    aliases: ["SOCAR", "СОКАР"],
    queries: ["SOCAR gas station in Kharkiv, Ukraine", "SOCAR АЗС Харків"]
  },
  {
    key: "ugo",
    aliases: ["U.GO", "UGO", "U GO", "ЮГО", "Ю.ГО"],
    queries: ["U.GO gas station in Kharkiv, Ukraine", "U.GO АЗС Харків"]
  },
  {
    key: "upg",
    aliases: ["UPG", "ЮПГ"],
    queries: ["UPG gas station in Kharkiv, Ukraine", "UPG АЗС Харків"]
  },
  {
    key: "brsmnafta",
    aliases: ["БРСМ-Нафта", "БРСМ", "BRSM", "BRSM-Nafta", "BRSМ"],
    queries: ["БРСМ-Нафта АЗС Харків", "BRSM gas station in Kharkiv, Ukraine"]
  },
  {
    key: "ukrnafta",
    aliases: ["Укрнафта", "Ukrnafta", "UKRNAFTA"],
    queries: ["Укрнафта АЗС Харків", "Ukrnafta gas station in Kharkiv, Ukraine"]
  },
  {
    key: "amic",
    aliases: ["AMIC", "Амік"],
    queries: ["AMIC gas station in Kharkiv, Ukraine", "AMIC АЗС Харків"]
  },
  {
    key: "shell",
    aliases: ["Shell", "Шелл"],
    queries: ["Shell gas station in Kharkiv, Ukraine", "Shell АЗС Харків"]
  },
  {
    key: "marshal",
    aliases: ["Marshal", "Маршал"],
    queries: ["Marshal gas station in Kharkiv, Ukraine", "Marshal АЗС Харків"]
  },
  {
    key: "ovis",
    aliases: ["Ovis", "Овіс"],
    queries: ["Ovis gas station in Kharkiv, Ukraine", "Ovis АЗС Харків"]
  },
  {
    key: "rodnik",
    aliases: ["Rodnik", "Родник"],
    queries: ["Rodnik gas station in Kharkiv, Ukraine", "Rodnik АЗС Харків"]
  },
  {
    key: "sunoil",
    aliases: ["SUN OIL", "Sun Oil", "Сан Ойл"],
    queries: ["SUN OIL gas station in Kharkiv, Ukraine", "SUN OIL АЗС Харків"]
  },
  {
    key: "avias",
    aliases: ["Авіас", "Avias", "АВІАС"],
    queries: ["Авіас АЗС Харків", "Avias gas station in Kharkiv, Ukraine"]
  },
  {
    key: "brentoil",
    aliases: ["Brent Oil", "Брент Ойл"],
    queries: ["Brent Oil gas station in Kharkiv, Ukraine", "Brent Oil АЗС Харків"]
  },
  {
    key: "dnipronafta",
    aliases: ["ДНІПРОНАФТА", "Дніпронафта", "DNIPRONAFTA"],
    queries: ["ДНІПРОНАФТА АЗС Харків", "DNIPRONAFTA gas station in Kharkiv, Ukraine"]
  }
];

const PROFILE_BY_ALIAS = new Map(
  NETWORK_PROFILES.flatMap(profile =>
    profile.aliases.map(alias => [normalizeNetworkName(alias), profile])
  )
);

let googleLoaderPromise = null;

export function normalizeNetworkName(value) {
  const lower = String(value || "")
    .trim()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase();

  const transliterated = Array.from(lower)
    .map(char => CYRILLIC_TO_LATIN[char] ?? char)
    .join("");

  return transliterated.replace(/[^a-z0-9]+/g, "");
}

export function buildNetworkPriceSummaries(stations) {
  const groups = new Map();

  for (const station of stations || []) {
    const name = String(station?.name || "").trim();
    const prices = Array.isArray(station?.prices) ? station.prices : [];
    if (!name || prices.length === 0) continue;

    const key = normalizeNetworkName(name);
    if (!key) continue;

    if (!groups.has(key)) {
      const profile = PROFILE_BY_ALIAS.get(key);
      groups.set(key, {
        key,
        name,
        aliases: profile?.aliases || [name],
        queries: profile?.queries || createGenericQueries(name),
        pricesByFuel: new Map()
      });
    }

    const group = groups.get(key);
    if (!group.aliases.some(alias => normalizeNetworkName(alias) === key)) {
      group.aliases = [...group.aliases, name];
    }

    for (const price of prices) {
      const fuelKey = price.fuelCode || price.fuelName;
      if (!fuelKey) continue;

      const current = group.pricesByFuel.get(fuelKey);
      if (!current || comparePriceFreshness(price, current) > 0) {
        group.pricesByFuel.set(fuelKey, price);
      }
    }
  }

  return [...groups.values()]
    .map(group => {
      const { pricesByFuel, ...network } = group;

      return {
        ...network,
        prices: [...pricesByFuel.values()].sort((a, b) => {
          const codeCompare = String(a.fuelCode || "").localeCompare(String(b.fuelCode || ""));
          return codeCompare || String(a.fuelName || "").localeCompare(String(b.fuelName || ""));
        })
      };
    })
    .sort((a, b) => a.name.localeCompare(b.name));
}

export async function searchGoogleFuelStations(networks, language = "uk") {
  if (!GOOGLE_MAPS_API_KEY) return [];

  const { Place } = await loadPlacesLibrary(language);
  const byPlaceId = new Map();

  for (const network of networks) {
    const places = await searchNetworkPlaces(Place, network, language);

    for (const place of places) {
      const station = toGoogleFuelStation(place, network);
      if (!station || byPlaceId.has(station.placeId)) continue;
      byPlaceId.set(station.placeId, station);
    }
  }

  return [...byPlaceId.values()].sort((a, b) =>
    a.networkName.localeCompare(b.networkName) || a.displayName.localeCompare(b.displayName)
  );
}

export function createDirectionsUrl(station) {
  const params = new URLSearchParams({
    api: "1",
    destination: `${station.latitude},${station.longitude}`,
    travelmode: "driving"
  });

  if (station.placeId) {
    params.set("destination_place_id", station.placeId);
  }

  return `https://www.google.com/maps/dir/?${params.toString()}`;
}

async function loadPlacesLibrary(language) {
  if (globalThis.google?.maps?.importLibrary) {
    return globalThis.google.maps.importLibrary("places");
  }

  if (!googleLoaderPromise) {
    googleLoaderPromise = import("@googlemaps/js-api-loader").then(loader => {
      loader.setOptions({
        key: GOOGLE_MAPS_API_KEY,
        region: "UA",
        language: normalizeLanguage(language)
      });

      return loader.importLibrary("places");
    });
  }

  return googleLoaderPromise;
}

async function searchNetworkPlaces(Place, network, language) {
  for (const query of network.queries) {
    const request = {
      textQuery: query,
      fields: PLACE_FIELDS,
      includedType: "gas_station",
      useStrictTypeFiltering: true,
      locationRestriction: KHARKIV_BOUNDS,
      language: normalizeLanguage(language),
      maxResultCount: 20,
      region: "ua"
    };

    const { places = [] } = await Place.searchByText(request);
    const filtered = places.filter(place => isUsableGoogleFuelStation(place, network));
    if (filtered.length > 0) return filtered;
  }

  return [];
}

function toGoogleFuelStation(place, network) {
  const coordinate = getPlaceCoordinate(place);
  const placeId = String(place.id || "").trim();
  if (!placeId || !coordinate) return null;

  return {
    id: placeId,
    placeId,
    networkKey: network.key,
    networkName: network.name,
    displayName: String(place.displayName || network.name).trim(),
    address: String(place.formattedAddress || "").trim(),
    googleMapsUri: String(place.googleMapsURI || "").trim(),
    latitude: coordinate.lat,
    longitude: coordinate.lng,
    primaryType: place.primaryType || "",
    types: Array.isArray(place.types) ? place.types : [],
    prices: network.prices || []
  };
}

function isUsableGoogleFuelStation(place, network) {
  const coordinate = getPlaceCoordinate(place);
  if (!place?.id || !coordinate) return false;

  const status = String(place.businessStatus || "").toLowerCase();
  if (status.includes("permanently")) return false;

  const types = new Set([place.primaryType, ...(place.types || [])].filter(Boolean));
  if (!types.has("gas_station")) return false;

  if (!isInsideKharkivBounds(coordinate)) return false;

  const candidateText = `${place.displayName || ""} ${place.formattedAddress || ""}`;
  return network.aliases.some(alias => {
    const normalizedAlias = normalizeNetworkName(alias);
    return normalizedAlias && normalizeNetworkName(candidateText).includes(normalizedAlias);
  });
}

function getPlaceCoordinate(place) {
  const location = place?.location;
  if (!location) return null;

  const lat = typeof location.lat === "function" ? location.lat() : Number(location.lat);
  const lng = typeof location.lng === "function" ? location.lng() : Number(location.lng);

  if (!Number.isFinite(lat) || !Number.isFinite(lng)) return null;
  return { lat, lng };
}

function isInsideKharkivBounds(coordinate) {
  return (
    coordinate.lat >= KHARKIV_BOUNDS.south &&
    coordinate.lat <= KHARKIV_BOUNDS.north &&
    coordinate.lng >= KHARKIV_BOUNDS.west &&
    coordinate.lng <= KHARKIV_BOUNDS.east
  );
}

function createGenericQueries(name) {
  const hasCyrillic = /[А-Яа-яІіЇїЄєҐґ]/.test(name);
  return hasCyrillic
    ? [`${name} АЗС Харків`, `${name} gas station in Kharkiv, Ukraine`]
    : [`${name} gas station in Kharkiv, Ukraine`, `${name} АЗС Харків`];
}

function comparePriceFreshness(left, right) {
  const leftDate = Date.parse(left?.date || "");
  const rightDate = Date.parse(right?.date || "");
  const dateDiff = (Number.isFinite(leftDate) ? leftDate : 0) - (Number.isFinite(rightDate) ? rightDate : 0);
  if (dateDiff !== 0) return dateDiff;
  return Number(left?.price || 0) - Number(right?.price || 0);
}

function normalizeLanguage(language) {
  return String(language || "uk").split("-")[0] || "uk";
}

export { KHARKIV_CENTER };
