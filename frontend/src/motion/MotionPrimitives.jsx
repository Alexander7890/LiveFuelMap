import { motion, useInView, useReducedMotion, useMotionValue, useSpring, useTransform } from "framer-motion";
import { useEffect, useMemo, useRef, useState } from "react";
import gsap from "gsap";
import { cx } from "../utils/format";
import { premiumEase, revealVariants, staggerContainer, staggerItem } from "./presets";

export function Reveal({
  as: Component = "div",
  direction = "up",
  delay = 0,
  once = true,
  amount = 0.18,
  className,
  children,
  ...props
}) {
  const reduceMotion = useReducedMotion();
  const MotionComponent = useMemo(() => motion(Component), [Component]);

  if (reduceMotion) {
    return <Component className={className} {...props}>{children}</Component>;
  }

  return (
    <MotionComponent
      custom={direction}
      variants={revealVariants}
      initial="hidden"
      whileInView="visible"
      viewport={{ once, amount }}
      transition={{ duration: 0.72, delay, ease: premiumEase }}
      className={className}
      {...props}
    >
      {children}
    </MotionComponent>
  );
}

export function Stagger({ as: Component = "div", className, children, once = true, amount = 0.16, ...props }) {
  const reduceMotion = useReducedMotion();
  const MotionComponent = useMemo(() => motion(Component), [Component]);

  if (reduceMotion) {
    return <Component className={className} {...props}>{children}</Component>;
  }

  return (
    <MotionComponent
      variants={staggerContainer}
      initial="hidden"
      whileInView="visible"
      viewport={{ once, amount }}
      className={className}
      {...props}
    >
      {children}
    </MotionComponent>
  );
}

export function StaggerItem({ as: Component = "div", className, children, ...props }) {
  const reduceMotion = useReducedMotion();
  const MotionComponent = useMemo(() => motion(Component), [Component]);

  if (reduceMotion) {
    return <Component className={className} {...props}>{children}</Component>;
  }

  return (
    <MotionComponent variants={staggerItem} className={className} {...props}>
      {children}
    </MotionComponent>
  );
}

export function SplitText({ text, as: Component = "span", by = "word", className, itemClassName, once = true }) {
  const reduceMotion = useReducedMotion();
  const rootRef = useRef(null);
  const parts = useMemo(() => {
    if (by === "char") return Array.from(text);
    return text.split(/(\s+)/).filter(part => part.length > 0);
  }, [by, text]);

  useEffect(() => {
    if (reduceMotion || !rootRef.current) return undefined;
    const ctx = gsap.context(() => {
      gsap.fromTo(
        ".split-text-unit",
        { yPercent: 120, opacity: 0, rotateX: -38, filter: "blur(8px)" },
        {
          yPercent: 0,
          opacity: 1,
          rotateX: 0,
          filter: "blur(0px)",
          duration: 0.78,
          stagger: by === "char" ? 0.018 : 0.055,
          ease: "power3.out",
          scrollTrigger: undefined
        }
      );
    }, rootRef);

    return () => ctx.revert();
  }, [by, reduceMotion, text]);

  if (reduceMotion) {
    return <Component className={className}>{text}</Component>;
  }

  return (
    <Component ref={rootRef} className={cx("inline-flex flex-wrap items-baseline gap-y-[0.06em]", className)}>
      {parts.map((part, index) => {
        const isSpace = /^\s+$/.test(part);
        return (
          <span
            key={`${part}-${index}`}
            className={cx(
              "inline-block align-baseline",
              isSpace
                ? "w-[0.28em] shrink-0"
                : "overflow-hidden px-[0.05em] pb-[0.16em] pt-[0.08em]",
              !isSpace && by !== "char" && "mr-[0.18em]"
            )}
          >
            {!isSpace && (
              <span className={cx("split-text-unit inline-block leading-[1.08] will-change-transform", itemClassName)}>
                {part}
              </span>
            )}
          </span>
        );
      })}
    </Component>
  );
}

export function HoverLetters({ children, className }) {
  const text = String(children);
  return (
    <span className={cx("inline-flex", className)} aria-label={text}>
      {Array.from(text).map((letter, index) => (
        <motion.span
          aria-hidden="true"
          key={`${letter}-${index}`}
          className="inline-block"
          whileHover={{ y: -3, color: "#0f8b8d" }}
          transition={{ duration: 0.22, ease: premiumEase }}
        >
          {letter === " " ? "\u00a0" : letter}
        </motion.span>
      ))}
    </span>
  );
}

export function CountUp({ value, suffix = "", decimals = 0, className }) {
  const ref = useRef(null);
  const inView = useInView(ref, { once: true, amount: 0.5 });
  const reduceMotion = useReducedMotion();
  const [display, setDisplay] = useState(0);

  useEffect(() => {
    if (!inView) return undefined;
    if (reduceMotion) {
      setDisplay(Number(value) || 0);
      return undefined;
    }

    const state = { value: 0 };
    const tween = gsap.to(state, {
      value: Number(value) || 0,
      duration: 1.4,
      ease: "power3.out",
      onUpdate: () => setDisplay(state.value)
    });

    return () => tween.kill();
  }, [inView, reduceMotion, value]);

  return (
    <span ref={ref} className={className}>
      {display.toFixed(decimals)}{suffix}
    </span>
  );
}

export function ParallaxGlow({ className, strength = 24 }) {
  const x = useMotionValue(0);
  const y = useMotionValue(0);
  const smoothX = useSpring(x, { stiffness: 70, damping: 24 });
  const smoothY = useSpring(y, { stiffness: 70, damping: 24 });
  const translateX = useTransform(smoothX, value => value / strength);
  const translateY = useTransform(smoothY, value => value / strength);

  useEffect(() => {
    const move = event => {
      x.set(event.clientX - window.innerWidth / 2);
      y.set(event.clientY - window.innerHeight / 2);
    };
    window.addEventListener("pointermove", move, { passive: true });
    return () => window.removeEventListener("pointermove", move);
  }, [x, y]);

  return (
    <motion.div
      aria-hidden="true"
      style={{ x: translateX, y: translateY }}
      className={cx("pointer-events-none absolute rounded-full blur-3xl", className)}
    />
  );
}
