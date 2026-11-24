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
 * Restrained architectural orbital visualization featuring the owner-approved
 * Zero-Interrupt Orbital Z (S1) centerpiece where both the twin Z arms and the
 * suspended central orbital 0 ring rotate continuously around the vertical (Y) axis
 * inside two restrained orbital tracks. Honors `prefers-reduced-motion: reduce`.
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

    const orbits: OrbitDef[] = [
      {
        rx: 158,
        ry: 150,
        tiltX: 1.12,
        tiltY: 0.22,
        tiltZ: -0.44,
        precessSpeed: 0.08,
        strokeRgb: "201, 164, 90", // Metallic Gold #C9A45A
        alpha: 0.55,
      },
      {
        rx: 134,
        ry: 128,
        tiltX: 0.92,
        tiltY: -0.34,
        tiltZ: 0.54,
        precessSpeed: -0.06,
        strokeRgb: "76, 179, 165", // Orbit Teal #4CB3A5
        alpha: 0.45,
        dashed: true,
      },
    ];

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

    // Exact S1 (Zero-Interrupt Orbital Z) coordinates on 128x128 master grid
    const s1UpperZ: [number, number][] = [
      [12.0, 12.0],
      [116.0, 12.0],
      [116.0, 23.82],
      [93.1, 46.72],
      [81.28, 34.9],
      [87.78, 28.4],
      [12.0, 28.4],
    ];

    const s1LowerZ: [number, number][] = [
      [116.0, 116.0],
      [12.0, 116.0],
      [12.0, 104.18],
      [34.9, 81.28],
      [46.72, 93.1],
      [40.22, 99.6],
      [116.0, 99.6],
    ];

    let animId = 0;
    let lastTime = performance.now();
    let elapsed = 0;

    // Extruded metallic shading helper so spinning bullion shapes retain depth even edge-on
    const makeGoldGradient = (rotY: number, isEdgeLayer: boolean) => {
      const facing = Math.abs(Math.cos(rotY));
      const grad = ctx.createLinearGradient(cx - 84, cy - 84, cx + 84, cy + 84);
      if (isEdgeLayer) {
        grad.addColorStop(0, "#8B6926");
        grad.addColorStop(0.5, "#6E511B");
        grad.addColorStop(1, "#4D3711");
      } else if (facing > 0.35) {
        grad.addColorStop(0, "#F3DFA6");
        grad.addColorStop(0.45, "#C9A45A");
        grad.addColorStop(1, "#94702C");
      } else {
        grad.addColorStop(0, "#DFC078");
        grad.addColorStop(0.5, "#B1893B");
        grad.addColorStop(1, "#7B5B20");
      }
      return grad;
    };

    const drawCoreRingLayer = (rotX: number, rotY: number, zDepth: number, isEdge: boolean) => {
      const s = 1.48;
      const ringSteps = 56;
      const proj = (gx: number, gy: number) =>
        project(rotatePoint3D((gx - 64) * s, (gy - 64) * s, zDepth, rotX, rotY, 0));

      ctx.beginPath();
      for (let i = 0; i <= ringSteps; i++) {
        const a = (i / ringSteps) * Math.PI * 2;
        const p = proj(64 + 24.3 * Math.cos(a), 64 + 24.3 * Math.sin(a));
        if (i === 0) ctx.moveTo(p.sx, p.sy);
        else ctx.lineTo(p.sx, p.sy);
      }
      ctx.closePath();
      for (let i = ringSteps; i >= 0; i--) {
        const a = (i / ringSteps) * Math.PI * 2;
        const p = proj(64 + 11.3 * Math.cos(a), 64 + 11.3 * Math.sin(a));
        if (i === ringSteps) ctx.moveTo(p.sx, p.sy);
        else ctx.lineTo(p.sx, p.sy);
      }
      ctx.closePath();
      ctx.fillStyle = makeGoldGradient(rotY, isEdge);
      ctx.fill("evenodd");
    };

    const drawZArmsLayer = (rotX: number, rotY: number, zDepth: number, isEdge: boolean) => {
      const s = 1.48;
      const proj = (gx: number, gy: number) =>
        project(rotatePoint3D((gx - 64) * s, (gy - 64) * s, zDepth, rotX, rotY, 0));

      ctx.beginPath();
      s1UpperZ.forEach(([x, y], i) => {
        const p = proj(x, y);
        if (i === 0) ctx.moveTo(p.sx, p.sy);
        else ctx.lineTo(p.sx, p.sy);
      });
      ctx.closePath();
      s1LowerZ.forEach(([x, y], i) => {
        const p = proj(x, y);
        if (i === 0) ctx.moveTo(p.sx, p.sy);
        else ctx.lineTo(p.sx, p.sy);
      });
      ctx.closePath();
      ctx.fillStyle = makeGoldGradient(rotY, isEdge);
      ctx.fill("nonzero");
    };

    const drawSpinningS1Mark = (rotX: number, armsRotY: number, coreRotY: number) => {
      ctx.save();
      const armsSign = Math.cos(armsRotY) >= 0 ? 1 : -1;
      const coreSign = Math.cos(coreRotY) >= 0 ? 1 : -1;
      const depths = [-3.2, -1.6, 0, 1.6, 3.2];

      // Twin Z arms spinning around the vertical (Y) axis
      for (let i = 0; i < depths.length; i++) {
        const d = depths[i] * armsSign;
        const isFrontFace = i === depths.length - 1;
        drawZArmsLayer(rotX, armsRotY, d, !isFrontFace);
      }

      // Suspended central orbital 0 ring counter-spinning around the vertical (Y) axis
      for (let i = 0; i < depths.length; i++) {
        const d = depths[i] * coreSign;
        const isFrontFace = i === depths.length - 1;
        drawCoreRingLayer(rotX, coreRotY, d, !isFrontFace);
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

      // Continuous vertical-axis (Y) spin for both the twin Z arms and suspended orbital 0 core
      const rotX = prefersReducedMotion ? 0.06 : Math.cos(elapsed * 0.35) * 0.06 + ptr.y * 0.16;
      const armsRotY = prefersReducedMotion ? 0.14 : elapsed * 0.55 + ptr.x * 0.4;
      const coreRotY = prefersReducedMotion ? -0.18 : -elapsed * 0.85 + ptr.x * 0.5;
      drawSpinningS1Mark(rotX, armsRotY, coreRotY);

      frontJobs.sort((a, b) => a.z - b.z);
      for (const j of frontJobs) j.draw();

      ctx.restore();
      if (!prefersReducedMotion && isVisible) {
        animId = requestAnimationFrame(renderFrame);
      }
    };

    let isVisible = true;
    const observer =
      typeof IntersectionObserver !== "undefined"
        ? new IntersectionObserver(
            (entries) => {
              const visible = Boolean(entries[0]?.isIntersecting);
              isVisible = visible;
              if (!visible && animId) {
                cancelAnimationFrame(animId);
                animId = 0;
              } else if (visible && !prefersReducedMotion && !animId) {
                lastTime = performance.now();
                animId = requestAnimationFrame(renderFrame);
              }
            },
            { threshold: 0.05 }
          )
        : null;
    observer?.observe(canvas);

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
      observer?.disconnect();
      if (animId) cancelAnimationFrame(animId);
      motionQuery?.removeEventListener("change", onMotionChange);
      canvas.removeEventListener("pointermove", onPointerMove);
      canvas.removeEventListener("pointerleave", onPointerLeave);
    };
  }, []);

  return (
    <div
      className={`relative flex items-center justify-center zar-panel overflow-hidden p-4 ${className}`}
    >
      <div
        className="absolute inset-0 zar-grid-backdrop opacity-30 pointer-events-none"
        aria-hidden="true"
      />

      <canvas
        ref={canvasRef}
        role="img"
        aria-label="انیمیشن مداری نماد ZARORBIT"
        className="w-full max-w-[390px] aspect-square block cursor-grab active:cursor-grabbing relative z-10"
      />

      <div className="absolute bottom-3 inset-x-4 flex items-center justify-between text-[10px] font-mono text-[#9CA3AF] pointer-events-none z-20 px-2 bidi-isolate">
        <span className="flex items-center gap-1.5 text-[#4CB3A5]">
          <span className="h-1.5 w-1.5 rounded-full bg-[#4CB3A5]" />
          ZARORBIT // ORBITAL CORE
        </span>
        <span>GOLD LEDGER • MULTI-ASSET VISION</span>
      </div>
    </div>
  );
}
