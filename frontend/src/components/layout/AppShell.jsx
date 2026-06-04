import { Link, NavLink } from "react-router-dom";
import { Activity, BarChart3, Database, ExternalLink, Fuel, LogOut, Menu, Shield, User, X } from "lucide-react";
import { AnimatePresence, motion } from "framer-motion";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useAuth } from "../../contexts/AuthContext";
import { useData } from "../../contexts/DataContext";
import ThemeMenu from "../common/ThemeMenu";
import LanguageSwitcher from "../common/LanguageSwitcher";
import AuthModal from "../profile/AuthModal";
import ProfileDrawer from "../profile/ProfileDrawer";
import ChatWidget from "../chat/ChatWidget";
import BrandMark from "../common/BrandMark";
import AdminContactBlock from "../common/AdminContactBlock";
import { Button } from "../common/Ui";
import { imageUrl } from "../../utils/format";
import { HoverLetters } from "../../motion/MotionPrimitives.jsx";
import { premiumEase } from "../../motion/presets.js";

const navItems = [
  { to: "/", labelKey: "nav.home", icon: Fuel },
  { to: "/reports", labelKey: "nav.reports", icon: BarChart3 },
  { to: "/admin", labelKey: "nav.admin", icon: Shield, admin: true }
];

export default function AppShell({ children }) {
  const { t } = useTranslation();
  const { currentUser, isAdmin, isLoggedIn, logout, profile } = useAuth();
  const { presence } = useData();
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
  const profileLabel =
    profile?.displayName ||
    profile?.nickname ||
    currentUser?.displayName ||
    currentUser?.nickname ||
    currentUser?.email ||
    t("common.profile");

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
        <div className="mx-auto flex max-w-[96rem] items-center gap-3 px-3 py-3 sm:px-6 lg:px-8">
          <Link to="/" className="group flex shrink-0 select-none items-center gap-2 sm:gap-3" draggable={false}>
            <BrandMark />
            <span className="select-none" draggable={false}>
              <span className="block whitespace-nowrap select-none text-base font-black tracking-tight text-slate-950 transition-colors duration-300 group-hover:text-brand-700 dark:text-white dark:group-hover:text-lime-200 sm:text-lg">
                <HoverLetters className="select-none">LiveFuelMap</HoverLetters>
              </span>
              <span className="hidden select-none whitespace-nowrap text-xs text-slate-500 dark:text-slate-300 2xl:block">{t("header.tagline")}</span>
            </span>
          </Link>

          <nav className="hidden min-w-0 flex-1 items-center justify-center gap-1 xl:flex">
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
                    <span data-active={isActive} className="inline-flex items-center gap-2 whitespace-nowrap">
                      <Icon className="h-4 w-4" />
                      {t(item.labelKey)}
                    </span>
                  )}
                </NavLink>
              );
            })}
          </nav>

          <div className="hidden shrink-0 items-center gap-2 xl:flex">
            <div className="badge-soft hidden 2xl:inline-flex">
              <Activity className="h-3.5 w-3.5" />
              {t("header.online", { count: presence?.totalConnections ?? 0 })}
            </div>
            <LanguageSwitcher compact />
            <ThemeMenu compact />
            {isLoggedIn ? (
              <motion.button
                whileHover={{ y: -2 }}
                type="button"
                onClick={() => setProfileOpen(true)}
                className="flex min-w-0 max-w-[280px] items-center gap-2 rounded-2xl border border-slate-200 bg-white/80 px-3 py-1.5 shadow-sm backdrop-blur transition hover:border-brand-300 dark:border-slate-800 dark:bg-slate-900/80 2xl:max-w-[340px]"
              >
                <img
                  src={imageUrl(profile?.profileImageUrl, "/default-station11.jpg")}
                  alt={profileLabel}
                  className="h-8 w-8 shrink-0 rounded-full object-cover"
                />
                <span className="min-w-0 truncate text-sm font-semibold">{profileLabel}</span>
              </motion.button>
            ) : (
              <Button onClick={() => setAuthOpen(true)}><User className="h-4 w-4" /> {t("common.login")}</Button>
            )}
          </div>

          <button className="ml-auto grid min-h-11 min-w-11 place-items-center rounded-xl p-2 transition hover:bg-slate-100 dark:hover:bg-slate-800 xl:hidden" onClick={() => setMobileOpen(true)} aria-label={t("nav.openMenu")}>
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
            className="fixed inset-0 z-[75] bg-slate-950/50 backdrop-blur-sm xl:hidden"
          >
            <motion.div
              initial={{ x: "100%" }}
              animate={{ x: 0 }}
              exit={{ x: "100%" }}
              transition={{ duration: 0.42, ease: premiumEase }}
              className="ml-auto h-full w-[min(390px,100vw)] overflow-y-auto bg-white/95 p-4 shadow-soft backdrop-blur-xl dark:bg-slate-950/95 sm:p-5"
            >
              <div className="flex items-center justify-between">
                <div className="font-bold">{t("nav.menu")}</div>
                <button onClick={() => setMobileOpen(false)} className="grid min-h-11 min-w-11 place-items-center rounded-full p-2 hover:bg-slate-100 dark:hover:bg-slate-800" aria-label={t("nav.closeMenu")}><X /></button>
              </div>
              <div className="mt-5 grid gap-2">
                {visibleNav.map(item => {
                  const Icon = item.icon;
                  return <NavLink key={item.to} to={item.to} onClick={() => setMobileOpen(false)} className="btn-secondary justify-start"><Icon className="h-4 w-4" />{t(item.labelKey)}</NavLink>;
                })}
              </div>
              <div className="mt-5"><LanguageSwitcher /></div>
              <div className="mt-5"><ThemeMenu /></div>
              <div className="mt-5 grid gap-2">
                {isLoggedIn ? (
                  <>
                    <Button variant="secondary" onClick={() => { setProfileOpen(true); setMobileOpen(false); }}><User className="h-4 w-4" /> {t("common.profile")}</Button>
                    <Button variant="danger" onClick={logout}><LogOut className="h-4 w-4" /> {t("common.logout")}</Button>
                  </>
                ) : (
                  <Button onClick={() => { setAuthOpen(true); setMobileOpen(false); }}>{t("common.login")}</Button>
                )}
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      <main>{children}</main>
      <footer className="mx-auto max-w-7xl px-3 py-8 text-sm text-slate-700 dark:text-slate-200 sm:px-6 lg:px-8">
        <div className="panel footer-panel grid gap-5 p-4 lg:grid-cols-[minmax(0,1fr)_minmax(320px,0.85fr)] lg:items-start">
          <div className="grid min-w-0 gap-2">
            <span className="font-semibold text-slate-900 dark:text-white">{t("footer.copyright")}</span>
            <span className="flex min-w-0 flex-wrap items-start gap-2 leading-6 text-slate-700 dark:text-slate-200">
              <Database className="mt-0.5 h-4 w-4 shrink-0 text-brand-600" />
              {t("footer.sourcePrefix")}
              <a className="inline-flex max-w-full items-center gap-1 break-all font-semibold text-brand-800 hover:underline dark:text-lime-200" href="https://www.minfin.com.ua/" target="_blank" rel="noreferrer">
                www.minfin.com.ua <ExternalLink className="h-3.5 w-3.5" />
              </a>
            </span>
          </div>
          <AdminContactBlock className="border-t border-slate-200 pt-4 dark:border-slate-800 lg:border-l lg:border-t-0 lg:pl-6 lg:pt-0" />
        </div>
      </footer>

      <AuthModal open={authOpen} onClose={() => setAuthOpen(false)} />
      <ProfileDrawer open={profileOpen} onClose={() => setProfileOpen(false)} />
      <ChatWidget />
    </div>
  );
}
