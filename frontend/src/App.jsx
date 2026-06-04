import { Navigate, Route, Routes, useLocation } from "react-router-dom";
import { AnimatePresence, motion } from "framer-motion";
import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import AppShell from "./components/layout/AppShell.jsx";
import HomePage from "./pages/HomePage.jsx";
import AdminPage from "./pages/AdminPage.jsx";
import ReportsPage from "./pages/ReportsPage.jsx";
import GoogleAuthCallbackPage from "./pages/GoogleAuthCallbackPage.jsx";
import SetupNicknamePage from "./pages/SetupNicknamePage.jsx";
import BrandPreloader from "./components/common/BrandPreloader.jsx";
import { useAuth } from "./contexts/AuthContext.jsx";
import { useSmoothScroll } from "./hooks/useSmoothScroll.js";
import { pageTransition } from "./motion/presets.js";

function ProtectedAdmin({ children }) {
  const { t } = useTranslation();
  const { loading, isAdmin } = useAuth();
  if (loading) return <div className="p-8 text-center text-slate-500">{t("common.loadingData")}</div>;
  return isAdmin ? children : <Navigate to="/" replace />;
}

function needsNicknameSetup(currentUser, profile) {
  return Boolean(profile?.requiresNicknameSetup ?? currentUser?.requiresNicknameSetup);
}

function NicknameSetupGate({ children }) {
  const location = useLocation();
  const { loading, isLoggedIn, currentUser, profile } = useAuth();
  const allowedPath = location.pathname === "/setup-nickname" || location.pathname === "/auth/google/callback";

  if (!loading && isLoggedIn && needsNicknameSetup(currentUser, profile) && !allowedPath) {
    return <Navigate to="/setup-nickname" replace />;
  }

  return children;
}

function SetupNicknameRoute() {
  const { t } = useTranslation();
  const { loading, isLoggedIn, currentUser, profile } = useAuth();

  if (loading) return <div className="p-8 text-center text-slate-500">{t("common.loadingData")}</div>;
  if (!isLoggedIn) return <Navigate to="/" replace />;
  if (!needsNicknameSetup(currentUser, profile)) return <Navigate to="/" replace />;

  return <AnimatedRoute><SetupNicknamePage /></AnimatedRoute>;
}

function AnimatedRoute({ children }) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 26, filter: "blur(12px)" }}
      animate={{ opacity: 1, y: 0, filter: "blur(0px)" }}
      exit={{ opacity: 0, y: -18, filter: "blur(8px)" }}
      transition={pageTransition}
    >
      {children}
    </motion.div>
  );
}

export default function App() {
  const location = useLocation();
  useSmoothScroll();

  useEffect(() => {
    if (window.__liveFuelMapLenis) {
      window.__liveFuelMapLenis.scrollTo(0, { immediate: false, duration: 0.78 });
      return;
    }
    window.scrollTo({ top: 0, behavior: "smooth" });
  }, [location.pathname]);

  return (
    <>
      <BrandPreloader />
      <AppShell>
        <NicknameSetupGate>
          <AnimatePresence mode="wait">
            <Routes location={location} key={location.pathname}>
              <Route path="/" element={<AnimatedRoute><HomePage /></AnimatedRoute>} />
              <Route path="/reports" element={<AnimatedRoute><ReportsPage /></AnimatedRoute>} />
              <Route path="/auth/google/callback" element={<AnimatedRoute><GoogleAuthCallbackPage /></AnimatedRoute>} />
              <Route path="/setup-nickname" element={<SetupNicknameRoute />} />
              <Route path="/admin" element={<ProtectedAdmin><AnimatedRoute><AdminPage /></AnimatedRoute></ProtectedAdmin>} />
              <Route path="/admin.html" element={<Navigate to="/admin" replace />} />
              <Route path="/index.html" element={<Navigate to="/" replace />} />
              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </AnimatePresence>
        </NicknameSetupGate>
      </AppShell>
    </>
  );
}
