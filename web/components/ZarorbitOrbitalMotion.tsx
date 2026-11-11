"use client";

import React, { useEffect, useRef } from "react";

interface OrbitDef {
  rx: number;
  ry: number;
  tiltX: number;
  tiltY: number;
  tiltZ: number;
  precessSpeed: number;
  strokeRgb: string;
  alpha: number;
  dashed?: boolean;
}

interface NetworkNode {
  orbitIdx: number;
  angle: number;
  speed: number;
  color: string;
  radius: number;
}

interface Vec3 {
  x: number;
  y: number;
  z: number;
}

function rotatePoint3D(
  x: number,
  y: number,
  z: number,
  rx: number,
  ry: number,
  rz: number
): Vec3 {
  const cy1 = Math.cos(rx);
  const sy1 = Math.sin(rx);
  const y1 = y * cy1 - z * sy1;
  const z1 = y * sy1 + z * cy1;
  const x1 = x;

  const cy2 = Math.cos(ry);
  const sy2 = Math.sin(ry);
  const x2 = x1 * cy2 + z1 * sy2;
  const z2 = -x1 * sy2 + z1 * cy2;
  const y2 = y1;

  const cz = Math.cos(rz);
  const sz = Math.sin(rz);
  return {
    x: x2 * cz - y2 * sz,
    y: x2 * sz + y2 * cz,
    z: z2,
  };
}

/**
 * Restrained architectural 1-0-1 orbital visualization aligned with
 * ZARORBIT Brand Book §8.2 & Visual Identity Guidelines v1.0 §11:
 * - Approved R1 (1–0–1) monogram centerpiece without Zcash stems
 * - Maximum of 2 clean orbital paths (Gold RWA axis + Orbit Teal interoperability axis)
 * - Minimal network participant nodes (no scattered 0/1 binary particles)
 * - Honors `prefers-reduced-motion: reduce`
 */
export default function ZarorbitOrbitalMotion({
  className = "",
}: {
  className?: string;
}) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const pointerRef = useRef<{
    x: number;
    y: number;
    targetX: number;
    targetY: number;
  }>({
    x: 0,
    y: 0,
    targetX: 0,
    targetY: 0,
  });

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    const motionQuery =
      typeof window !== "undefined" && typeof window.matchMedia === "function"
        ? window.matchMedia("(prefers-reduced-motion: reduce)")
        : null;
    let prefersReducedMotion = Boolean(motionQuery?.matches);

    const logicalSize = 420;
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    canvas.width = logicalSize * dpr;
    canvas.height = logicalSize * dpr;

    const cx = logicalSize / 2;
    const cy = logicalSize / 2;
    const fov = 680;

    // Maximum two restrained orbital paths per Brand Book §8.2
    const orbits: OrbitDef[] = [
      {
        rx: 156,
        ry: 148,
        tiltX: 1.12,
        tiltY: 0.22,
        tiltZ: -0.44,
        precessSpeed: 0.08,
        strokeRgb: "201, 164, 90", // Metallic Gold #C9A45A
        alpha: 0.58,
      },
      {
        rx: 132,
        ry: 126,
        tiltX: 0.92,
        tiltY: -0.34,
        tiltZ: 0.54,
        precessSpeed: -0.06,
        strokeRgb: "76, 179, 165", // Orbit Teal #4CB3A5
        alpha: 0.45,
        dashed: true,
      },
    ];

    // Minimal participant nodes along the two orbital paths
    const nodes: NetworkNode[] = [
      { orbitIdx: 0, angle: 0.4, speed: 0.28, color: "#C9A45A", radius: 4.5 },
      { orbitIdx: 0, angle: 3.54, speed: 0.28, color: "#F2EEE5", radius: 3.2 },
      { orbitIdx: 1, angle: 2.0, speed: -0.22, color: "#4CB3A5", radius: 4.5 },
    ];

    const project = (v: Vec3) => {
      const scale = fov / (fov - v.z);
      return {
        sx: cx + v.x * scale,
        sy: cy + v.y * scale,
        sz: v.z,
        scale,
      };
    };

    let animId = 0;
    let lastTime = performance.now();
    let elapsed = 0;

    const drawR1Monogram = (rotX: number, rotY: number) => {
      // Scale factor puts 128x128 R1 artwork neatly centered around origin (64,64)
      const s = 1.48;
      const project2D = (gx: number, gy: number) => {
        const v = rotatePoint3D((gx - 64) * s, (gy - 64) * s, 0, rotX, rotY, 0);
        return project(v);
      };

      // 1. Central tilted elliptical "0" orbit (-56 deg) sampled in 3D space
      const ellPts: { sx: number; sy: number }[] = [];
      const steps = 64;
      const radAngle = (-56 * Math.PI) / 180;
      const cosA = Math.cos(radAngle);
      const sinA = Math.sin(radAngle);
      for (let i = 0; i <= steps; i++) {
        const t = (i / steps) * Math.PI * 2;
        const ex = 38.0 * Math.cos(t);
        const ey = 14.5 * Math.sin(t);
        const gx = 64 + ex * cosA - ey * sinA;
        const gy = 64 + ex * sinA + ey * cosA;
        ellPts.push(project2D(gx, gy));
      }

      ctx.save();
      ctx.beginPath();
      ellPts.forEach((pt, idx) => {
        if (idx === 0) ctx.moveTo(pt.sx, pt.sy);
        else ctx.lineTo(pt.sx, pt.sy);
      });
      ctx.closePath();
      ctx.strokeStyle = "#C9A45A";
      ctx.lineWidth = 10.5 * s;
      ctx.stroke();

      // Subtle inner Ivory highlight along orbital core
      ctx.strokeStyle = "rgba(242, 238, 229, 0.22)";
      ctx.lineWidth = 1.2;
      ctx.stroke();

      // 2. Top chamfered horizontal "1" bar: M 37 24 H 94 V 36 H 28 L 26 34 Z
      const topBar: [number, number][] = [
        [37, 24],
        [94, 24],
        [94, 36],
        [28, 36],
        [26, 34],
      ];
      // Bottom chamfered horizontal "1" bar: M 34 92 H 100 L 102 94 L 91 104 H 34 Z
      const botBar: [number, number][] = [
        [34, 92],
        [100, 92],
        [102, 94],
        [91, 104],
        [34, 104],
      ];

      ctx.fillStyle = "#C9A45A";
      for (const bar of [topBar, botBar]) {
        ctx.beginPath();
        bar.forEach(([x, y], i) => {
          const p = project2D(x, y);
          if (i === 0) ctx.moveTo(p.sx, p.sy);
          else ctx.lineTo(p.sx, p.sy);
        });
        ctx.closePath();
        ctx.fill();
      }
      ctx.restore();
    };

    const renderFrame = (now: number) => {
      const dt = prefersReducedMotion ? 0 : Math.min((now - lastTime) / 1000, 0.05);
      lastTime = now;
      elapsed += dt;

      const ptr = pointerRef.current;
      ptr.x += (ptr.targetX - ptr.x) * 0.08;
      ptr.y += (ptr.targetY - ptr.y) * 0.08;

      ctx.save();
      ctx.scale(dpr, dpr);
      ctx.clearRect(0, 0, logicalSize, logicalSize);

      // Restrained Obsidian radial depth (no heavy neon aura)
      const bgGlow = ctx.createRadialGradient(cx, cy, 16, cx, cy, 180);
      bgGlow.addColorStop(0, "rgba(201, 164, 90, 0.08)");
      bgGlow.addColorStop(0.55, "rgba(76, 179, 165, 0.04)");
      bgGlow.addColorStop(1, "rgba(23, 25, 29, 0)");
      ctx.fillStyle = bgGlow;
      ctx.fillRect(0, 0, logicalSize, logicalSize);

      type DrawJob = { z: number; draw: () => void };
      const backJobs: DrawJob[] = [];
      const frontJobs: DrawJob[] = [];

      const getOrbitPoint = (o: OrbitDef, theta: number): Vec3 => {
        const lx = o.rx * Math.cos(theta);
        const ly = o.ry * Math.sin(theta);
        return rotatePoint3D(
          lx,
          ly,
          0,
          o.tiltX + ptr.y * 0.14,
          o.tiltY + elapsed * o.precessSpeed + ptr.x * 0.14,
          o.tiltZ
        );
      };

      const SEGMENTS = 56;
      for (const o of orbits) {
        let prev = getOrbitPoint(o, 0);
        for (let s = 1; s <= SEGMENTS; s++) {
          if (o.dashed && s % 2 === 0) {
            prev = getOrbitPoint(o, (s / SEGMENTS) * Math.PI * 2);
            continue;
          }
          const theta = (s / SEGMENTS) * Math.PI * 2;
          const curr = getOrbitPoint(o, theta);
          const p0 = project(prev);
          const p1 = project(curr);
          const midZ = (prev.z + curr.z) * 0.5;
          const depthFactor = Math.max(0.24, Math.min(1, (midZ + 170) / 340));
          const alpha = o.alpha * depthFactor;

          const job: DrawJob = {
            z: midZ,
            draw: () => {
              ctx.strokeStyle = `rgba(${o.strokeRgb}, ${alpha.toFixed(3)})`;
              ctx.lineWidth = 1.25;
              ctx.beginPath();
              ctx.moveTo(p0.sx, p0.sy);
              ctx.lineTo(p1.sx, p1.sy);
              ctx.stroke();
            },
          };

          if (midZ < 0) backJobs.push(job);
          else frontJobs.push(job);
          prev = curr;
        }
      }

      for (const node of nodes) {
        node.angle += node.speed * dt;
        const pos = getOrbitPoint(orbits[node.orbitIdx], node.angle);
        const proj = project(pos);
        const job: DrawJob = {
          z: pos.z,
          draw: () => {
            ctx.beginPath();
            ctx.arc(proj.sx, proj.sy, node.radius * proj.scale, 0, Math.PI * 2);
            ctx.fillStyle = node.color;
            ctx.fill();
          },
        };
        if (pos.z < 0) backJobs.push(job);
        else frontJobs.push(job);
      }

      backJobs.sort((a, b) => a.z - b.z);
      for (const j of backJobs) j.draw();

      const rotY = prefersReducedMotion ? 0.14 : Math.sin(elapsed * 0.5) * 0.18 + ptr.x * 0.22;
      const rotX = prefersReducedMotion ? 0.08 : Math.cos(elapsed * 0.4) * 0.08 + ptr.y * 0.18;
      drawR1Monogram(rotX, rotY);

      frontJobs.sort((a, b) => a.z - b.z);
      for (const j of frontJobs) j.draw();

      ctx.restore();
      if (!prefersReducedMotion) {
        animId = requestAnimationFrame(renderFrame);
      }
    };

    renderFrame(performance.now());

    const onPointerMove = (e: PointerEvent) => {
      const rect = canvas.getBoundingClientRect();
      if (!rect.width || !rect.height) return;
      const nx = ((e.clientX - rect.left) / rect.width - 0.5) * 2;
      const ny = ((e.clientY - rect.top) / rect.height - 0.5) * 2;
      pointerRef.current.targetX = Math.max(-1, Math.min(1, nx));
      pointerRef.current.targetY = Math.max(-1, Math.min(1, ny));
      if (prefersReducedMotion) renderFrame(performance.now());
    };

    const onPointerLeave = () => {
      pointerRef.current.targetX = 0;
      pointerRef.current.targetY = 0;
      if (prefersReducedMotion) renderFrame(performance.now());
    };

    const onMotionChange = (e: MediaQueryListEvent) => {
      prefersReducedMotion = e.matches;
      if (prefersReducedMotion) {
        if (animId) {
          cancelAnimationFrame(animId);
          animId = 0;
        }
        renderFrame(performance.now());
      } else if (!animId) {
        lastTime = performance.now();
        animId = requestAnimationFrame(renderFrame);
      }
    };

    motionQuery?.addEventListener("change", onMotionChange);
    canvas.addEventListener("pointermove", onPointerMove);
    canvas.addEventListener("pointerleave", onPointerLeave);

    return () => {
      if (animId) cancelAnimationFrame(animId);
      motionQuery?.removeEventListener("change", onMotionChange);
      canvas.removeEventListener("pointermove", onPointerMove);
      canvas.removeEventListener("pointerleave", onPointerLeave);
    };
  }, []);

  return (
    <div
      className={`relative flex items-center justify-center rounded-[20px] bg-[#1F2329] border border-[#2B303A] shadow-card overflow-hidden p-4 ${className}`}
    >
      <div
        className="absolute inset-0 zar-grid-backdrop opacity-30 pointer-events-none"
        aria-hidden="true"
      />

      <canvas
        ref={canvasRef}
        role="img"
        aria-label="نمودار تعاملی مونوگرام ۱-۰-۱ و مدار پیوند دارایی‌های واقعی و دیجیتال ZARORBIT"
        className="w-full max-w-[390px] aspect-square block cursor-grab active:cursor-grabbing relative z-10"
      />

      <div className="absolute bottom-3 inset-x-4 flex items-center justify-between text-[10px] font-mono text-[#9CA3AF] pointer-events-none z-20 px-2 bidi-isolate">
        <span className="flex items-center gap-1.5 text-[#4CB3A5]">
          <span className="h-1.5 w-1.5 rounded-full bg-[#4CB3A5]" />
          ZARORBIT // 1–0–1 ORBIT
        </span>
        <span>RWA • DIGITAL • DAO</span>
      </div>
    </div>
  );
}
