"use client";

import React, { useEffect, useRef } from "react";

interface OrbitDef {
  rx: number;
  ry: number;
  tiltX: number;
  tiltY: number;
  tiltZ: number;
  precessSpeed: number;
  lineWidth: number;
  alpha: number;
}

interface BinaryParticle {
  orbitIdx: number;
  digit: "0" | "1";
  angle: number;
  speed: number;
  radialOffset: number;
  normalOffset: number;
  baseSize: number;
  pulsePhase: number;
  highlight: boolean;
}

interface Vec3 {
  x: number;
  y: number;
  z: number;
}

// Zarorbit stylized 'Z' outer perimeter (clockwise in screen coords, normalized [-100, 100])
const Z_OUTER_PTS: [number, number][] = [
  [-58, -68],
  [-9, -68],
  [-9, -94],
  [9, -94],
  [9, -68],
  [58, -68],
  [58, -42],
  [-4, 42],
  [58, 42],
  [58, 68],
  [9, 68],
  [9, 94],
  [-9, 94],
  [-9, 68],
  [-58, 68],
  [-58, 42],
  [4, -42],
  [-58, -42],
];

// Inner diagonal slot cutout (counter-clockwise in screen coords so edge normals face into void)
const Z_HOLE_PTS: [number, number][] = [
  [26, -42],
  [-36, 42],
  [-26, 42],
  [36, -42],
];

function rotatePoint3D(
  x: number,
  y: number,
  z: number,
  rx: number,
  ry: number,
  rz: number
): Vec3 {
  // Rotate around X
  const cy1 = Math.cos(rx);
  const sy1 = Math.sin(rx);
  const y1 = y * cy1 - z * sy1;
  const z1 = y * sy1 + z * cy1;
  const x1 = x;

  // Rotate around Y
  const cy2 = Math.cos(ry);
  const sy2 = Math.sin(ry);
  const x2 = x1 * cy2 + z1 * sy2;
  const z2 = -x1 * sy2 + z1 * cy2;
  const y2 = y1;

  // Rotate around Z
  const cz = Math.cos(rz);
  const sz = Math.sin(rz);
  return {
    x: x2 * cz - y2 * sz,
    y: x2 * sz + y2 * cz,
    z: z2,
  };
}

export default function ZarorbitOrbitalMotion({
  className = "",
}: {
  className?: string;
}) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const pointerRef = useRef<{ x: number; y: number; targetX: number; targetY: number }>({
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

    const logicalSize = 420;
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    canvas.width = logicalSize * dpr;
    canvas.height = logicalSize * dpr;

    const cx = logicalSize / 2;
    const cy = logicalSize / 2;
    const fov = 620;

    const orbits: OrbitDef[] = [
      {
        rx: 158,
        ry: 152,
        tiltX: 1.14,
        tiltY: 0.25,
        tiltZ: -0.42,
        precessSpeed: 0.14,
        lineWidth: 1.25,
        alpha: 0.55,
      },
      {
        rx: 174,
        ry: 162,
        tiltX: 1.18,
        tiltY: -0.35,
        tiltZ: 0.52,
        precessSpeed: -0.11,
        lineWidth: 1.1,
        alpha: 0.48,
      },
      {
        rx: 188,
        ry: 176,
        tiltX: 1.32,
        tiltY: 0.08,
        tiltZ: 0.15,
        precessSpeed: 0.08,
        lineWidth: 1.0,
        alpha: 0.42,
      },
      {
        rx: 144,
        ry: 138,
        tiltX: 0.78,
        tiltY: 0.48,
        tiltZ: -0.82,
        precessSpeed: -0.17,
        lineWidth: 0.95,
        alpha: 0.38,
      },
    ];

    // Generate randomized 0 & 1 binary particles across the orbits
    const particles: BinaryParticle[] = [];
    const countsPerOrbit = [10, 10, 9, 7];
    countsPerOrbit.forEach((count, orbitIdx) => {
      const direction = orbitIdx % 2 === 0 ? 1 : -1;
      for (let i = 0; i < count; i++) {
        particles.push({
          orbitIdx,
          digit: Math.random() > 0.48 ? "1" : "0",
          angle: (i / count) * Math.PI * 2 + (Math.random() - 0.5) * 0.45,
          speed: direction * (0.42 + Math.random() * 0.38),
          radialOffset: (Math.random() - 0.5) * 14,
          normalOffset: (Math.random() - 0.5) * 10,
          baseSize: 11 + Math.random() * 5.5,
          pulsePhase: Math.random() * Math.PI * 2,
          highlight: Math.random() > 0.65,
        });
      }
    });

    // Subtle background ambient gold dust specks
    const dust = Array.from({ length: 28 }, () => ({
      x: Math.random() * logicalSize,
      y: Math.random() * logicalSize,
      r: 0.7 + Math.random() * 1.4,
      phase: Math.random() * Math.PI * 2,
      speed: 0.6 + Math.random() * 1.2,
    }));

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

    const renderFrame = (now: number) => {
      const dt = Math.min((now - lastTime) / 1000, 0.05);
      lastTime = now;
      elapsed += dt;

      // Smooth pointer parallax easing
      const ptr = pointerRef.current;
      ptr.x += (ptr.targetX - ptr.x) * 0.08;
      ptr.y += (ptr.targetY - ptr.y) * 0.08;

      ctx.save();
      ctx.scale(dpr, dpr);
      ctx.clearRect(0, 0, logicalSize, logicalSize);

      // Soft central ambient gold radial glow
      const bgGlow = ctx.createRadialGradient(cx, cy, 12, cx, cy, 195);
      bgGlow.addColorStop(0, "rgba(229, 184, 11, 0.16)");
      bgGlow.addColorStop(0.45, "rgba(212, 175, 55, 0.05)");
      bgGlow.addColorStop(1, "rgba(8, 10, 15, 0)");
      ctx.fillStyle = bgGlow;
      ctx.fillRect(0, 0, logicalSize, logicalSize);

      // Draw drifting gold specks
      for (const s of dust) {
        const twinkle = 0.2 + 0.45 * (0.5 + 0.5 * Math.sin(elapsed * s.speed + s.phase));
        ctx.fillStyle = `rgba(253, 224, 71, ${twinkle.toFixed(3)})`;
        ctx.beginPath();
        ctx.arc(s.x, s.y, s.r, 0, Math.PI * 2);
        ctx.fill();
      }

      // Queue for depth-sorted drawing (Back orbit segments / particles -> 3D Z -> Front segments / particles)
      type DrawJob = { z: number; draw: () => void };
      const backJobs: DrawJob[] = [];
      const frontJobs: DrawJob[] = [];

      // Transform orbit point by orbit angles + pointer parallax
      const getOrbitPoint = (o: OrbitDef, theta: number, rOff = 0, nOff = 0): Vec3 => {
        const lx = (o.rx + rOff) * Math.cos(theta);
        const ly = (o.ry + rOff) * Math.sin(theta);
        const lz = nOff;
        return rotatePoint3D(
          lx,
          ly,
          lz,
          o.tiltX + ptr.y * 0.18,
          o.tiltY + elapsed * o.precessSpeed + ptr.x * 0.18,
          o.tiltZ
        );
      };

      // 1. Build Orbital Ring Segments
      const SEGMENTS = 56;
      for (let oi = 0; oi < orbits.length; oi++) {
        const o = orbits[oi];
        let prev = getOrbitPoint(o, 0);
        for (let s = 1; s <= SEGMENTS; s++) {
          const theta = (s / SEGMENTS) * Math.PI * 2;
          const curr = getOrbitPoint(o, theta);
          const p0 = project(prev);
          const p1 = project(curr);
          const midZ = (prev.z + curr.z) * 0.5;
          const depthFactor = Math.max(0.18, Math.min(1, (midZ + 190) / 380));
          const alpha = o.alpha * depthFactor;
          const lw = o.lineWidth * (0.65 + 0.55 * depthFactor);

          const job: DrawJob = {
            z: midZ,
            draw: () => {
              ctx.strokeStyle = `rgba(235, 193, 72, ${alpha.toFixed(3)})`;
              ctx.lineWidth = lw;
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

      // 2. Build Binary 0 / 1 Particles
      for (const p of particles) {
        p.angle += p.speed * dt;
        const o = orbits[p.orbitIdx];
        const pos = getOrbitPoint(o, p.angle, p.radialOffset, p.normalOffset);
        const proj = project(pos);
        const depthNorm = Math.max(0, Math.min(1, (pos.z + 195) / 390));
        const pulse = 0.85 + 0.15 * Math.sin(elapsed * 2.4 + p.pulsePhase);
        const alpha = (0.28 + 0.72 * depthNorm) * pulse;
        const fontPx = p.baseSize * proj.scale * (0.75 + 0.35 * depthNorm);

        const job: DrawJob = {
          z: pos.z,
          draw: () => {
            ctx.save();
            ctx.font = `700 ${fontPx.toFixed(1)}px ui-monospace, SFMono-Regular, Menlo, monospace`;
            ctx.textAlign = "center";
            ctx.textBaseline = "middle";

            if (p.highlight && depthNorm > 0.52) {
              ctx.shadowColor = "rgba(253, 224, 71, 0.85)";
              ctx.shadowBlur = 10 * depthNorm;
              ctx.fillStyle = `rgba(255, 245, 190, ${alpha.toFixed(3)})`;
            } else {
              ctx.fillStyle = `rgba(229, 184, 11, ${alpha.toFixed(3)})`;
            }
            ctx.fillText(p.digit, proj.sx, proj.sy);
            ctx.restore();
          },
        };

        if (pos.z < 0) backJobs.push(job);
        else frontJobs.push(job);
      }

      // 3. Draw back half (behind central Z)
      backJobs.sort((a, b) => a.z - b.z);
      for (const j of backJobs) j.draw();

      // 4. Draw Central 3D Self-Rotating Brushed Gold Zarorbit 'Z'
      const zScale = 0.62;
      const halfDepth = 11;
      const rotY = elapsed * 0.92 + ptr.x * 0.35;
      const rotX = 0.14 + Math.sin(elapsed * 0.7) * 0.06 + ptr.y * 0.25;
      const floatY = Math.sin(elapsed * 1.4) * 3.5;

      const transformZVert = (px: number, py: number, pz: number) => {
        const v = rotatePoint3D(px * zScale, py * zScale + floatY, pz, rotX, rotY, 0);
        return { v, p: project(v) };
      };

      const outerFront = Z_OUTER_PTS.map(([x, y]) => transformZVert(x, y, halfDepth));
      const outerBack = Z_OUTER_PTS.map(([x, y]) => transformZVert(x, y, -halfDepth));
      const holeFront = Z_HOLE_PTS.map(([x, y]) => transformZVert(x, y, halfDepth));
      const holeBack = Z_HOLE_PTS.map(([x, y]) => transformZVert(x, y, -halfDepth));

      // Determine cap normal (+z local rotated) to know whether front or back face points toward camera
      const frontNormal = rotatePoint3D(0, 0, 1, rotX, rotY, 0);
      const showFrontCap = frontNormal.z >= 0;

      // Build visible side extrusion quads for outer wall and inner slot wall
      interface SideQuad {
        z: number;
        pts: { sx: number; sy: number }[];
        shade: number;
      }
      const sideQuads: SideQuad[] = [];

      const collectRingSides = (
        pts2D: [number, number][],
        frontArr: ReturnType<typeof transformZVert>[],
        backArr: ReturnType<typeof transformZVert>[]
      ) => {
        const n = pts2D.length;
        for (let i = 0; i < n; i++) {
          const j = (i + 1) % n;
          const dx = pts2D[j][0] - pts2D[i][0];
          const dy = pts2D[j][1] - pts2D[i][1];
          const len = Math.hypot(dx, dy) || 1;
          // Outward normal in screen coords (x right, y down) for CW outer / CCW hole
          const lnx = dy / len;
          const lny = -dx / len;
          const wNormal = rotatePoint3D(lnx, lny, 0, rotX, rotY, 0);
          if (wNormal.z > -0.08) {
            const avgZ =
              (frontArr[i].v.z +
                frontArr[j].v.z +
                backArr[j].v.z +
                backArr[i].v.z) *
              0.25;
            // Directional lighting from upper-left front
            const light = Math.max(
              0.2,
              Math.min(
                1,
                0.55 + wNormal.x * -0.25 + wNormal.y * -0.35 + wNormal.z * 0.35
              )
            );
            sideQuads.push({
              z: avgZ,
              pts: [frontArr[i].p, frontArr[j].p, backArr[j].p, backArr[i].p],
              shade: light,
            });
          }
        }
      };

      collectRingSides(Z_OUTER_PTS, outerFront, outerBack);
      collectRingSides(Z_HOLE_PTS, holeFront, holeBack);

      sideQuads.sort((a, b) => a.z - b.z);
      for (const q of sideQuads) {
        const r = Math.round(130 + 110 * q.shade);
        const g = Math.round(92 + 95 * q.shade);
        const b = Math.round(14 + 36 * q.shade);
        ctx.fillStyle = `rgb(${r}, ${g}, ${b})`;
        ctx.strokeStyle = `rgba(255, 226, 130, ${(0.25 * q.shade).toFixed(2)})`;
        ctx.lineWidth = 0.6;
        ctx.beginPath();
        ctx.moveTo(q.pts[0].sx, q.pts[0].sy);
        ctx.lineTo(q.pts[1].sx, q.pts[1].sy);
        ctx.lineTo(q.pts[2].sx, q.pts[2].sy);
        ctx.lineTo(q.pts[3].sx, q.pts[3].sy);
        ctx.closePath();
        ctx.fill();
        ctx.stroke();
      }

      // Draw camera-facing cap of Z
      const activeOuter = showFrontCap ? outerFront : outerBack;
      const activeHole = showFrontCap ? holeFront : holeBack;

      ctx.save();
      ctx.beginPath();
      activeOuter.forEach((pt, idx) => {
        if (idx === 0) ctx.moveTo(pt.p.sx, pt.p.sy);
        else ctx.lineTo(pt.p.sx, pt.p.sy);
      });
      ctx.closePath();

      activeHole.forEach((pt, idx) => {
        if (idx === 0) ctx.moveTo(pt.p.sx, pt.p.sy);
        else ctx.lineTo(pt.p.sx, pt.p.sy);
      });
      ctx.closePath();

      // Dynamic sheen shifts across brushed gold face as Z rotates
      const sheenShift = Math.sin(rotY) * 55;
      const capGrad = ctx.createLinearGradient(
        cx - 65 + sheenShift,
        cy - 75,
        cx + 65 + sheenShift,
        cy + 75
      );
      const faceBrightness = Math.max(0.45, Math.abs(frontNormal.z));
      if (faceBrightness > 0.7) {
        capGrad.addColorStop(0, "#FFF3B0");
        capGrad.addColorStop(0.28, "#F2C94C");
        capGrad.addColorStop(0.55, "#D4AF37");
        capGrad.addColorStop(0.82, "#F9E076");
        capGrad.addColorStop(1, "#9E7410");
      } else {
        capGrad.addColorStop(0, "#E5B80B");
        capGrad.addColorStop(0.5, "#B78618");
        capGrad.addColorStop(1, "#78530A");
      }

      ctx.shadowColor = "rgba(229, 184, 11, 0.38)";
      ctx.shadowBlur = 18;
      ctx.fillStyle = capGrad;
      ctx.fill("evenodd");

      // Crisp specular gold bevel edge
      ctx.shadowBlur = 0;
      ctx.strokeStyle = "rgba(255, 244, 194, 0.72)";
      ctx.lineWidth = 1.1;
      ctx.stroke();
      ctx.restore();

      // 5. Draw front half (in front of central Z)
      frontJobs.sort((a, b) => a.z - b.z);
      for (const j of frontJobs) j.draw();

      ctx.restore();
      animId = requestAnimationFrame(renderFrame);
    };

    animId = requestAnimationFrame(renderFrame);

    const onPointerMove = (e: PointerEvent) => {
      const rect = canvas.getBoundingClientRect();
      if (!rect.width || !rect.height) return;
      const nx = ((e.clientX - rect.left) / rect.width - 0.5) * 2;
      const ny = ((e.clientY - rect.top) / rect.height - 0.5) * 2;
      pointerRef.current.targetX = Math.max(-1, Math.min(1, nx));
      pointerRef.current.targetY = Math.max(-1, Math.min(1, ny));
    };

    const onPointerLeave = () => {
      pointerRef.current.targetX = 0;
      pointerRef.current.targetY = 0;
    };

    canvas.addEventListener("pointermove", onPointerMove);
    canvas.addEventListener("pointerleave", onPointerLeave);

    return () => {
      cancelAnimationFrame(animId);
      canvas.removeEventListener("pointermove", onPointerMove);
      canvas.removeEventListener("pointerleave", onPointerLeave);
    };
  }, []);

  return (
    <div
      className={`relative flex items-center justify-center rounded-3xl bg-gradient-to-b from-[#111622]/90 via-[#0a0d14]/95 to-[#06080d]/95 border border-amber-500/30 shadow-goldGlow overflow-hidden p-4 ${className}`}
    >
      {/* Ambient corner glows */}
      <div className="absolute -top-20 -right-20 w-48 h-48 bg-amber-500/15 rounded-full blur-3xl pointer-events-none" />
      <div className="absolute -bottom-20 -left-20 w-48 h-48 bg-amber-600/10 rounded-full blur-3xl pointer-events-none" />

      <canvas
        ref={canvasRef}
        aria-label="انیمیشن سه‌بعدی گردش مدار اعداد باینری به دور نشان زرین زَر اوربیت"
        className="w-full max-w-[400px] aspect-square block cursor-grab active:cursor-grabbing relative z-10"
      />

      {/* Bottom HUD caption */}
      <div className="absolute bottom-3 inset-x-4 flex items-center justify-between text-[10px] font-mono text-amber-300/75 pointer-events-none z-20 px-2">
        <span className="flex items-center gap-1.5">
          <span className="h-1.5 w-1.5 rounded-full bg-emerald-400 animate-pulse" />
          BINARY ORBIT ENGINE
        </span>
        <span>01 // LEDGER SYNC</span>
      </div>
    </div>
  );
}
