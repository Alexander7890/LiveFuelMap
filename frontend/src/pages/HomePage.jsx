import { motion, useReducedMotion, useScroll, useTransform } from "framer-motion";
import { ArrowDown, Fuel, Sparkles, TrendingUp } from "lucide-react";
import { useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button, Section, SkeletonGrid } from "../components/common/Ui";
import StationCarousel from "../components/stations/StationCarousel";
import FuelCalculator from "../components/fuel/FuelCalculator";
import PriceChart from "../components/fuel/PriceChart";
import StationComparison from "../components/fuel/StationComparison";
import CommentsSection from "../components/comments/CommentsSection";
import MapSection from "../components/map/MapSection";
import { useData } from "../contexts/DataContext";
import { CountUp, Reveal, SplitText, Stagger, StaggerItem } from "../motion/MotionPrimitives.jsx";
import { scrollToTarget } from "../hooks/useSmoothScroll.js";
import { premiumEase } from "../motion/presets.js";

export default function HomePage() {
  const { t } = useTranslation();
  const { loading, presence, stations, fuels } = useData();
  const [activeFuel, setActiveFuel] = useState("all");
  const [commentStationFilter, setCommentStationFilter] = useState(null);
  const heroRef = useRef(null);
  const commentsRef = useRef(null);
  const reduceMotion = useReducedMotion();
  const { scrollYProgress } = useScroll({
    target: heroRef,
    offset: ["start start", "end start"]
  });
  const heroMediaY = useTransform(scrollYProgress, [0, 1], reduceMotion ? [0, 0] : [0, 72]);
  const heroMediaScale = useTransform(scrollYProgress, [0, 1], reduceMotion ? [1, 1] : [1, 0.94]);
  const heroTextY = useTransform(scrollYProgress, [0, 1], reduceMotion ? [0, 0] : [0, -34]);

  function showComments(stationId) {
    setCommentStationFilter(stationId);
    window.setTimeout(() => scrollToTarget(commentsRef.current), 50);
  }

  return (
    <>
      <section ref={heroRef} className="hero-cinema relative isolate overflow-hidden lg:min-h-[calc(100svh-72px)]">
        <span className="pointer-events-none absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-brand-300/70 to-transparent" />
        <div className="mx-auto grid max-w-7xl items-center gap-8 px-3 py-10 sm:px-6 sm:py-14 lg:min-h-[calc(100svh-72px)] lg:grid-cols-[1.03fr_.97fr] lg:gap-10 lg:px-8">
          <motion.div style={{ y: heroTextY }} className="relative z-10 flex flex-col justify-center">
            <Reveal>
              <Stagger className="mb-6 flex flex-wrap gap-2">
                <StaggerItem className="badge-soft"><Sparkles className="h-3.5 w-3.5" /> {t("home.badges.fresh")}</StaggerItem>
                <StaggerItem className="badge-soft">{t("home.badges.online", { count: presence?.totalConnections ?? 0 })}</StaggerItem>
                <StaggerItem className="badge-soft">{t("home.badges.stations", { count: stations.length || 0 })}</StaggerItem>
              </Stagger>
              <h1 className="max-w-5xl text-balance text-4xl font-black leading-[1.02] tracking-tight text-slate-950 dark:text-white sm:text-6xl sm:leading-[0.98] lg:text-7xl xl:text-8xl">
                <SplitText text={t("home.heroTitle")} />
              </h1>
              <motion.p
                initial={{ opacity: 0, y: 22, filter: "blur(10px)" }}
                animate={{ opacity: 1, y: 0, filter: "blur(0px)" }}
                transition={{ delay: 0.72, duration: 0.86, ease: premiumEase }}
                className="mt-7 max-w-2xl text-lg leading-8 text-slate-600 dark:text-slate-300 sm:text-xl"
              >
                {t("home.heroText")}
              </motion.p>
              <motion.div
                initial={{ opacity: 0, y: 18 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: 0.94, duration: 0.66, ease: premiumEase }}
                className="mt-8 grid gap-3 sm:mt-9 sm:flex sm:flex-wrap"
              >
                <Button onClick={() => scrollToTarget("#stations")}><Fuel className="h-4 w-4" /> {t("home.goStations")}</Button>
                <Button variant="secondary" onClick={() => scrollToTarget("#chart")}><ArrowDown className="h-4 w-4" /> {t("home.priceChart")}</Button>
              </motion.div>
            </Reveal>
          </motion.div>

          <motion.div
            style={{ y: heroMediaY, scale: heroMediaScale }}
            className="hero-device relative z-10 mx-auto w-full max-w-[620px]"
          >
            <Reveal direction="right" delay={0.12} className="panel premium-card overflow-hidden p-3 sm:p-4">
              <div className="relative aspect-[4/5] overflow-hidden rounded-[1.75rem] bg-slate-950 text-white shadow-[0_32px_100px_rgba(15,47,51,.26)] sm:aspect-[5/4]">
                <video
                  className="absolute inset-0 h-full w-full object-cover opacity-[.12] saturate-125"
                  src="/IMG_2443.MP4"
                  autoPlay
                  muted
                  loop
                  playsInline
                  poster="/favicon-32x32.png"
                />
                <span className="absolute inset-0 bg-[radial-gradient(circle_at_50%_20%,rgba(174,255,41,.16),transparent_18rem),linear-gradient(180deg,rgba(2,6,23,.58),rgba(2,6,23,.96))]" />
                <div className="absolute inset-0 flex flex-col justify-between p-5 sm:p-7">
                  <div className="flex items-center justify-between">
                    <span className="rounded-full bg-white/12 px-3 py-1 text-xs font-semibold uppercase tracking-[0.22em] backdrop-blur">{t("home.kharkivLive")}</span>
                    <Sparkles className="h-6 w-6 text-lime-200" />
                  </div>
                  <div>
                    <div className="text-3xl font-black tracking-tight drop-shadow-[0_12px_28px_rgba(0,0,0,.55)] sm:text-5xl">{t("home.kharkivRegion")}</div>
                    <p className="mt-3 max-w-md text-sm leading-6 text-white/82 drop-shadow-[0_8px_18px_rgba(0,0,0,.45)] sm:text-base">{t("home.heroCardText")}</p>
                    <div className="mt-6 grid grid-cols-3 gap-2 sm:gap-3">
                      <div className="rounded-2xl bg-white/12 p-3 backdrop-blur">
                        <div className="text-2xl font-black"><CountUp value={stations.length || 0} /></div>
                        <div className="text-xs text-white/70">{t("common.stations")}</div>
                      </div>
                      <div className="rounded-2xl bg-white/12 p-3 backdrop-blur">
                        <div className="text-2xl font-black"><CountUp value={fuels.length || 0} /></div>
                        <div className="text-xs text-white/70">{t("home.fuelTypeCount")}</div>
                      </div>
                      <div className="rounded-2xl bg-white/12 p-3 backdrop-blur">
                        <div className="flex items-center gap-1 text-2xl font-black"><TrendingUp className="h-5 w-5" /><CountUp value={presence?.totalConnections ?? 0} /></div>
                        <div className="text-xs text-white/70">{t("home.onlineShort")}</div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </Reveal>
          </motion.div>
        </div>
      </section>

      <Section id="stations" title={t("home.sections.stationsTitle")} subtitle={t("home.sections.stationsSubtitle")}>
        {loading ? <SkeletonGrid /> : <StationCarousel activeFuel={activeFuel} setActiveFuel={setActiveFuel} onShowComments={showComments} />}
      </Section>

      <Section title={t("home.sections.calculatorTitle")} subtitle={t("home.sections.calculatorSubtitle")}>
        <FuelCalculator />
      </Section>

      <Section id="chart" title={t("home.sections.analyticsTitle")} subtitle={t("home.sections.analyticsSubtitle")}>
        <div className="grid gap-5">
          <PriceChart activeFuel={activeFuel} />
          <StationComparison />
        </div>
      </Section>

      <Section title={t("home.sections.commentsTitle")} subtitle={t("home.sections.commentsSubtitle")}>
        <div ref={commentsRef}>
          <CommentsSection stationFilter={commentStationFilter} setStationFilter={setCommentStationFilter} />
        </div>
      </Section>

      <Section id="map" title={t("home.sections.mapTitle")} subtitle={t("home.sections.mapSubtitle")}>
        <MapSection />
      </Section>
    </>
  );
}
