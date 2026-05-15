import { useEffect } from "react";
import Lenis from "lenis";

export function useSmoothScroll() {
  useEffect(() => {
    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (reduceMotion) return undefined;

    const lenis = new Lenis({
      duration: 1.12,
      easing: t => Math.min(1, 1.001 - Math.pow(2, -10 * t)),
      smoothWheel: true,
      wheelMultiplier: 0.88,
      touchMultiplier: 1.15
    });

    let frameId = 0;
    const raf = time => {
      lenis.raf(time);
      frameId = requestAnimationFrame(raf);
    };

    frameId = requestAnimationFrame(raf);
    window.__liveFuelMapLenis = lenis;

    return () => {
      cancelAnimationFrame(frameId);
      lenis.destroy();
      if (window.__liveFuelMapLenis === lenis) {
        delete window.__liveFuelMapLenis;
      }
    };
  }, []);
}

export function scrollToTarget(target, options = {}) {
  const element = typeof target === "string" ? document.querySelector(target) : target;
  if (!element) return;

  if (window.__liveFuelMapLenis) {
    window.__liveFuelMapLenis.scrollTo(element, { offset: -88, duration: 1.05, ...options });
    return;
  }

  element.scrollIntoView({ behavior: "smooth", block: "start" });
}
