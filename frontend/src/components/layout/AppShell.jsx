import { Link, NavLink } from "react-router-dom";
import { Activity, BarChart3, Database, ExternalLink, Fuel, LogOut, Menu, Shield, User, X } from "lucide-react";
import { AnimatePresence, motion } from "framer-motion";
import { useEffect, useState } from "react";
import { useAuth } from "../../contexts/AuthContext";
import { useData } from "../../contexts/DataContext";
import ThemeMenu from "../common/ThemeMenu";
import AuthModal from "../profile/AuthModal";
import ProfileDrawer from "../profile/ProfileDrawer";
import ChatWidget from "../chat/ChatWidget";
import BrandMark from "../common/BrandMark";
import { Button } from "../common/Ui";
import { imageUrl } from "../../utils/format";
import { HoverLetters } from "../../motion/MotionPrimitives.jsx";
import { premiumEase } from "../../motion/presets.js";

const navItems = [
  { to: "/", label: "Головна", icon: Fuel },
  { to: "/reports", label: "Експорт", icon: BarChart3 },
  { to: "/admin", label: "Адмін", icon: Shield, admin: true }
];

export default function AppShell({ children }) {
  const { currentUser, isAdmin, isLoggedIn, logout, profile } = useAuth();
  const { presence, connected } = useData();
  const [authOpen, setAuthOpen] = useState(false);
  const [profileOpen, setProfileOpen] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [scrolled, setScrolled] = useState(false);

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 18);
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  const visibleNav = navItems.filter(item => !item.admin || isAdmin);

  return (
    <div className="app-shell">
      <motion.header
        initial={{ y: -24, opacity: 0 }}
        animate={{ y: 0, opacity: 1 }}
        transition={{ duration: 0.55, ease: premiumEase }}
        className={`sticky top-0 z-50 select-none border-b backdrop-blur-2xl transition-all duration-500 ${
          scrolled
            ? "border-slate-200/80 bg-white/90 shadow-panel dark:border-slate-800 dark:bg-slate-950/90"
            : "border-transparent bg-white/60 dark:bg-slate-950/40"
        }`}
      >
        <div className="mx-auto flex max-w-7xl items-center gap-3 px-4 py-3 sm:px-6 lg:px-8">
          <Link to="/" className="group flex select-none items-center gap-3" draggable={false}>
            <BrandMark />
            <span className="select-none" draggable={false}>
              <span className="block select-none text-lg font-black tracking-tight">
                <HoverLetters className="select-none">LiveFuelMap</HoverLetters>
              </span>
              <span className="hidden select-none text-xs text-slate-500 dark:text-slate-300 sm:block">Моніторинг цін на пальне</span>
            </span>
          </Link>

          <nav className="ml-auto hidden items-center gap-1 lg:flex">
            {visibleNav.map(item => {
              const Icon = item.icon;
              return (
                <NavLink
                  key={item.to}
                  to={item.to}
                  className={({ isActive }) =>
                    `premium-link inline-flex items-center gap-2 rounded-xl px-3 py-2 text-sm font-semibold transition duration-300 ${
                      isActive
                        ? "bg-brand-50 text-brand-700 dark:bg-brand-500/15 dark:text-brand-100"
                        : "text-slate-600 hover:bg-slate-100/80 dark:text-slate-300 dark:hover:bg-slate-800/80"
                    }`
                  }
                >
                  {({ isActive }) => (
                    <span data-active={isActive} className="inline-flex items-center gap-2">
                      <Icon className="h-4 w-4" />
                      {item.label}
                    </span>
                  )}
                </NavLink>
              );
            })}
          </nav>

          <div className="ml-auto hidden items-center gap-2 lg:flex">
            <div className="badge-soft">
              <Activity className="h-3.5 w-3.5" />
              {connected ? "online" : "offline"} · {presence?.totalConnections ?? 0}
            </div>
            <ThemeMenu />
            {isLoggedIn ? (
              <motion.button
                whileHover={{ y: -2 }}
                type="button"
                onClick={() => setProfileOpen(true)}
                className="flex items-center gap-2 rounded-2xl border border-slate-200 bg-white/80 px-2 py-1.5 shadow-sm backdrop-blur transition hover:border-brand-300 dark:border-slate-800 dark:bg-slate-900/80"
              >
                <img
                  src={imageUrl(profile?.profileImageUrl, "/default-station11.jpg")}
                  alt={currentUser?.nickname || currentUser?.email}
                  className="h-8 w-8 rounded-full object-cover"
                />
                <span className="max-w-[160px] truncate text-sm font-semibold">{profile?.nickname || currentUser?.email}</span>
              </motion.button>
            ) : (
              <Button onClick={() => setAuthOpen(true)}><User className="h-4 w-4" /> Увійти</Button>
            )}
          </div>

          <button className="ml-auto rounded-xl p-2 transition hover:bg-slate-100 dark:hover:bg-slate-800 lg:hidden" onClick={() => setMobileOpen(true)}>
            <Menu className="h-6 w-6" />
          </button>
        </div>
      </motion.header>

      <AnimatePresence>
        {mobileOpen && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-[75] bg-slate-950/50 backdrop-blur-sm lg:hidden"
          >
            <motion.div
              initial={{ x: "100%" }}
              animate={{ x: 0 }}
              exit={{ x: "100%" }}
              transition={{ duration: 0.42, ease: premiumEase }}
              className="ml-auto h-full w-[min(360px,88vw)] bg-white/95 p-5 shadow-soft backdrop-blur-xl dark:bg-slate-950/95"
            >
              <div className="flex items-center justify-between">
                <div className="font-bold">Меню</div>
                <button onClick={() => setMobileOpen(false)} className="rounded-full p-2 hover:bg-slate-100 dark:hover:bg-slate-800"><X /></button>
              </div>
              <div className="mt-5 grid gap-2">
                {visibleNav.map(item => {
                  const Icon = item.icon;
                  return <NavLink key={item.to} to={item.to} onClick={() => setMobileOpen(false)} className="btn-secondary justify-start"><Icon className="h-4 w-4" />{item.label}</NavLink>;
                })}
              </div>
              <div className="mt-5"><ThemeMenu /></div>
              <div className="mt-5 grid gap-2">
                {isLoggedIn ? (
                  <>
                    <Button variant="secondary" onClick={() => { setProfileOpen(true); setMobileOpen(false); }}><User className="h-4 w-4" /> Профіль</Button>
                    <Button variant="danger" onClick={logout}><LogOut className="h-4 w-4" /> Вийти</Button>
                  </>
                ) : (
                  <Button onClick={() => { setAuthOpen(true); setMobileOpen(false); }}>Увійти</Button>
                )}
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      <main>{children}</main>
      <footer className="mx-auto max-w-7xl px-4 py-8 text-sm text-slate-700 dark:text-slate-200 sm:px-6 lg:px-8">
        <div className="panel footer-panel grid gap-4 p-4 lg:grid-cols-[1fr_auto] lg:items-center">
          <div className="grid gap-2">
            <span className="font-semibold text-slate-900 dark:text-white">© 2026 LiveFuelMap. Усі права захищено. Проєкт розроблено студентом НТУ «ХПІ» групи КН-1022в — Костіним О.В.</span>
            <span className="flex items-start gap-2 leading-6 text-slate-700 dark:text-slate-200">
              <Database className="mt-0.5 h-4 w-4 shrink-0 text-brand-600" />
              Дані про пальне взяті з сайту
              <a className="inline-flex items-center gap-1 font-semibold text-brand-800 hover:underline dark:text-lime-200" href="https://www.minfin.com.ua/" target="_blank" rel="noreferrer">
                www.minfin.com.ua <ExternalLink className="h-3.5 w-3.5" />
              </a>
            </span>
          </div>
          <div className="max-w-md border-t border-slate-200 pt-3 text-slate-600 dark:border-slate-800 dark:text-slate-300 lg:justify-self-end lg:border-l lg:border-t-0 lg:pl-6 lg:pt-0">
            <span className="block font-semibold text-slate-900 dark:text-white">LiveFuelMap підтримує</span>
            <span className="block leading-6">AI-чат, систему real-time оновлення даних,</span>
            <span className="block leading-6">експорт даних та функціональну адмін-панель</span>
          </div>
        </div>
      </footer>

      <AuthModal open={authOpen} onClose={() => setAuthOpen(false)} />
      <ProfileDrawer open={profileOpen} onClose={() => setProfileOpen(false)} />
      <ChatWidget />
    </div>
  );
}
