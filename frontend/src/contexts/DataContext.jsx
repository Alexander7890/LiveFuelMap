import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { api } from "../services/api";
import { createFuelHubConnection } from "../services/signalr";
import { useToast } from "./ToastContext";
import { formatPrice } from "../utils/format";

const DataContext = createContext(null);

export function DataProvider({ children }) {
  const { showToast } = useToast();
  const [fuels, setFuels] = useState([]);
  const [stations, setStations] = useState([]);
  const [comments, setComments] = useState([]);
  const [presence, setPresence] = useState(null);
  const [connected, setConnected] = useState(false);
  const [loading, setLoading] = useState(true);
  const hubRef = useRef(null);

  const applyFuelPayload = useCallback(payload => {
    if (!payload) return;
    setFuels(payload.fuels || []);
    setStations(payload.stations || payload.items || []);
  }, []);

  const loadFuelData = useCallback(async () => {
    const [fuelItems, stationResult] = await Promise.all([
      api.fuels(),
      api.stations({ pageSize: 200 })
    ]);
    setFuels(fuelItems || []);
    setStations(stationResult.items || stationResult || []);
  }, []);

  const loadComments = useCallback(async stationId => {
    const items = await api.comments.list(stationId);
    if (!stationId) setComments(items || []);
    return items || [];
  }, []);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        await Promise.all([loadFuelData(), loadComments()]);
      } finally {
        if (active) setLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [loadFuelData, loadComments]);

  useEffect(() => {
    const connection = createFuelHubConnection();
    hubRef.current = connection;

    connection.on("fuelDataUpdate", payload => {
      applyFuelPayload(payload);
      showToast("Дані оновлено", "Ціни та АЗС синхронізовано в реальному часі.", "info");
    });

    connection.on("priceChanged", change => {
      const direction = change.changeType === "decrease" ? "знизилась" : "підвищилась";
      const oldPrice = change.oldPrice == null ? "—" : `${formatPrice(change.oldPrice)} грн`;
      showToast(
        `Ціна ${direction}`,
        `${change.stationName}: ${change.fuelName} ${oldPrice} → ${formatPrice(change.newPrice)} грн`,
        change.changeType === "decrease" ? "success" : "warning"
      );
    });

    connection.on("commentCreated", comment => {
      setComments(current => [comment, ...current.filter(item => item.id !== comment.id)]);
    });

    connection.on("commentUpdated", comment => {
      setComments(current => current.map(item => (item.id === comment.id ? comment : item)));
    });

    connection.on("commentDeleted", payload => {
      setComments(current => current.filter(item => item.id !== payload.id));
    });

    const updatePresence = status => setPresence(status);
    connection.on("onlineStatus", updatePresence);
    connection.on("onlineStatusChanged", updatePresence);

    connection.onreconnected(() => setConnected(true));
    connection.onreconnecting(() => setConnected(false));
    connection.onclose(() => setConnected(false));

    connection.start()
      .then(async () => {
        setConnected(true);
        try {
          const data = await connection.invoke("GetFuelData");
          applyFuelPayload(data);
          const hubComments = await connection.invoke("GetComments", null);
          setComments(hubComments || []);
          setPresence(await connection.invoke("GetOnlineStatus"));
        } catch {
          // REST fallback already loaded the page data.
        }
      })
      .catch(() => setConnected(false));

    return () => {
      connection.stop();
      hubRef.current = null;
    };
  }, [applyFuelPayload, showToast]);

  const sendComment = useCallback(async request => {
    if (hubRef.current?.state === "Connected") {
      return hubRef.current.invoke("SendComment", request);
    }
    throw new Error("WebSocket тимчасово недоступний.");
  }, []);

  const updateOwnComment = useCallback(async (id, request) => {
    if (hubRef.current?.state === "Connected") {
      return hubRef.current.invoke("UpdateComment", id, request);
    }
    return api.comments.update(id, request);
  }, []);

  const deleteOwnComment = useCallback(async id => {
    if (hubRef.current?.state === "Connected") {
      await hubRef.current.invoke("DeleteComment", id);
      return;
    }
    await api.comments.delete(id);
  }, []);

  const value = useMemo(() => ({
    fuels,
    stations,
    comments,
    presence,
    connected,
    loading,
    reloadFuelData: loadFuelData,
    loadComments,
    sendComment,
    updateOwnComment,
    deleteOwnComment,
    setStations,
    setComments
  }), [fuels, stations, comments, presence, connected, loading, loadFuelData, loadComments, sendComment, updateOwnComment, deleteOwnComment]);

  return <DataContext.Provider value={value}>{children}</DataContext.Provider>;
}

export function useData() {
  const context = useContext(DataContext);
  if (!context) throw new Error("useData must be used inside DataProvider");
  return context;
}

