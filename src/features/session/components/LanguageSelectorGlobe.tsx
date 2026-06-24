// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { LanguageDefinition, ALL_LANGUAGES, hasSharedFlag } from '../../../core/config/languages';
import { getLanguageGlobeLocation, type GlobeLocation } from '../config/languageGlobeLocations';
import LanguageScrollWheel from './LanguageScrollWheel';

interface LanguageSelectorGlobeProps {
    nativeLangCode: string | null;
    targetLangCode: string | null;
    onSelectNative: (code: string | null) => void;
    onSelectTarget: (code: string | null) => void;
    onCancel?: () => void;
    onInteract: () => void;
}

interface Rotation {
    lng: number;
    lat: number;
}

interface Vector3 {
    x: number;
    y: number;
    z: number;
}

interface GlobeLanguagePoint {
    lang: LanguageDefinition;
    location: GlobeLocation;
    vector: Vector3;
}

interface ProjectedPoint {
    x: number;
    y: number;
    z: number;
    screenX: number;
    screenY: number;
    centerDistance: number;
}

interface RoutePaths {
    pathD: string;
    planePathD: string;
}

const DEG_TO_RAD = Math.PI / 180;
const GLOBE_RADIUS_PERCENT = 43.5;
const MAX_LAT_ROTATION = Math.PI * 0.47;
const EARTH_TEXTURE_URL = '/globe-image-background/Flag_map_of_the_world-1024.webp';

const VERTEX_SHADER_SOURCE = `
attribute vec2 a_position;
varying vec2 v_uv;

void main() {
  v_uv = (a_position + 1.0) * 0.5;
  gl_Position = vec4(a_position, 0.0, 1.0);
}
`;

const FRAGMENT_SHADER_SOURCE = `
precision mediump float;

varying vec2 v_uv;
uniform sampler2D u_map;
uniform vec2 u_rotation;
uniform float u_time;
uniform float u_hasTexture;

const float PI = 3.141592653589793;

float saturate(float value) {
  return clamp(value, 0.0, 1.0);
}

vec3 rotateViewToWorld(vec3 normal) {
  float cosLat = cos(u_rotation.y);
  float sinLat = sin(u_rotation.y);
  float cosLng = cos(u_rotation.x);
  float sinLng = sin(u_rotation.x);

  vec3 xUndone = vec3(
    normal.x,
    normal.y * cosLat + normal.z * sinLat,
    -normal.y * sinLat + normal.z * cosLat
  );

  return vec3(
    xUndone.x * cosLng - xUndone.z * sinLng,
    xUndone.y,
    xUndone.x * sinLng + xUndone.z * cosLng
  );
}

float proceduralLand(vec2 geoUv, vec3 world) {
  float lng = (geoUv.x - 0.5) * PI * 2.0;
  float lat = (0.5 - geoUv.y) * PI;
  float continentalNoise =
    sin(lng * 2.0 + sin(lat * 3.0)) * 0.34 +
    sin(lng * 4.5 - lat * 1.7) * 0.22 +
    sin(lng * 8.0 + lat * 4.2) * 0.10;
  float midLatitudeBias = cos(lat * 1.2) * 0.42;
  float islands = smoothstep(0.78, 0.95, sin(lng * 13.0) * sin(lat * 9.0));
  return saturate(smoothstep(0.10, 0.58, continentalNoise + midLatitudeBias) + islands * 0.16);
}

void main() {
  vec2 disk = v_uv * 2.0 - 1.0;
  float radiusSq = dot(disk, disk);

  if (radiusSq > 1.0) {
    discard;
  }

  vec3 viewNormal = normalize(vec3(disk.x, disk.y, sqrt(max(0.0, 1.0 - radiusSq))));
  vec3 world = normalize(rotateViewToWorld(viewNormal));

  float lat = asin(clamp(world.y, -1.0, 1.0));
  float lng = atan(world.x, world.z);
  vec2 geoUv = vec2(fract(0.5 + lng / (PI * 2.0)), 0.5 - lat / PI);

  vec3 sampled = texture2D(u_map, geoUv).rgb;
  float sampledBrightness = max(max(sampled.r, sampled.g), sampled.b);
  float sampledLand = smoothstep(0.075, 0.18, sampledBrightness);
  float fallbackLand = proceduralLand(geoUv, world);
  float landMask = mix(fallbackLand, saturate(max(sampledLand, fallbackLand * 0.32)), u_hasTexture);

  vec3 ocean = mix(vec3(0.010, 0.090, 0.170), vec3(0.020, 0.260, 0.420), 0.5 + world.y * 0.35);
  vec3 landBase = vec3(0.125, 0.355, 0.205);
  vec3 landTint = mix(landBase, vec3(sampledBrightness), 0.08);
  vec3 color = mix(ocean, landTint, landMask);

  float time = u_time * 0.00004;
  float cloudBand =
    sin((lng + time) * 8.0 + lat * 2.5) *
    sin((lng - time * 1.7) * 4.0 - lat * 5.0);
  float cloudMask = smoothstep(0.64, 0.96, cloudBand) * (1.0 - landMask * 0.35);
  color = mix(color, vec3(0.82, 0.90, 0.94), cloudMask * 0.13);

  vec3 keyLight = normalize(vec3(-0.42, 0.34, 0.84));
  float directLight = saturate(dot(viewNormal, keyLight));
  vec3 sunWorld = normalize(vec3(cos(u_time * 0.000018), 0.16, sin(u_time * 0.000018)));
  float daySide = smoothstep(-0.35, 0.78, dot(world, sunWorld));
  color *= 0.45 + directLight * 0.62;
  color *= 0.56 + daySide * 0.44;

  float rim = smoothstep(0.54, 1.0, radiusSq);
  color += vec3(0.12, 0.42, 0.72) * rim * 0.32;

  float glint = smoothstep(0.990, 1.0, dot(viewNormal, normalize(vec3(-0.36, 0.20, 0.91))));
  color += vec3(0.65, 0.86, 1.0) * glint * 0.24;

  gl_FragColor = vec4(color, 1.0);
}
`;

function clamp(value: number, min: number, max: number): number {
    return Math.max(min, Math.min(max, value));
}

function clamp01(value: number): number {
    return clamp(value, 0, 1);
}

function smoothStep(edge0: number, edge1: number, value: number): number {
    const t = clamp01((value - edge0) / (edge1 - edge0));
    return t * t * (3 - 2 * t);
}

function normalizeAngle(angle: number): number {
    const fullTurn = Math.PI * 2;
    return ((angle + Math.PI) % fullTurn + fullTurn) % fullTurn - Math.PI;
}

function shortestAngleDelta(from: number, to: number): number {
    return normalizeAngle(to - from);
}

function clampRotation(rotation: Rotation): Rotation {
    return {
        lng: normalizeAngle(rotation.lng),
        lat: clamp(rotation.lat, -MAX_LAT_ROTATION, MAX_LAT_ROTATION),
    };
}

function latLngToVector(location: GlobeLocation): Vector3 {
    const lat = location.lat * DEG_TO_RAD;
    const lng = location.lng * DEG_TO_RAD;
    const cosLat = Math.cos(lat);

    return {
        x: cosLat * Math.sin(lng),
        y: Math.sin(lat),
        z: cosLat * Math.cos(lng),
    };
}

function rotateVector(vector: Vector3, rotation: Rotation): Vector3 {
    const cosLng = Math.cos(rotation.lng);
    const sinLng = Math.sin(rotation.lng);
    const cosLat = Math.cos(rotation.lat);
    const sinLat = Math.sin(rotation.lat);

    const x1 = vector.x * cosLng + vector.z * sinLng;
    const y1 = vector.y;
    const z1 = -vector.x * sinLng + vector.z * cosLng;

    return {
        x: x1,
        y: y1 * cosLat - z1 * sinLat,
        z: y1 * sinLat + z1 * cosLat,
    };
}

function projectVector(vector: Vector3, rotation: Rotation): ProjectedPoint {
    const rotated = rotateVector(vector, rotation);

    return {
        ...rotated,
        screenX: 50 + rotated.x * GLOBE_RADIUS_PERCENT,
        screenY: 50 - rotated.y * GLOBE_RADIUS_PERCENT,
        centerDistance: Math.hypot(rotated.x, rotated.y),
    };
}

function normalizeVector(vector: Vector3): Vector3 {
    const length = Math.hypot(vector.x, vector.y, vector.z) || 1;
    return {
        x: vector.x / length,
        y: vector.y / length,
        z: vector.z / length,
    };
}

function slerpVector(from: Vector3, to: Vector3, progress: number): Vector3 {
    const dot = clamp(from.x * to.x + from.y * to.y + from.z * to.z, -1, 1);
    const theta = Math.acos(dot);
    const sinTheta = Math.sin(theta);

    if (sinTheta < 0.0001) {
        return normalizeVector({
            x: from.x + (to.x - from.x) * progress,
            y: from.y + (to.y - from.y) * progress,
            z: from.z + (to.z - from.z) * progress,
        });
    }

    const fromScale = Math.sin((1 - progress) * theta) / sinTheta;
    const toScale = Math.sin(progress * theta) / sinTheta;

    return {
        x: from.x * fromScale + to.x * toScale,
        y: from.y * fromScale + to.y * toScale,
        z: from.z * fromScale + to.z * toScale,
    };
}

function pathFromPoints(points: ProjectedPoint[]): string {
    return points
        .map((point, index) => `${index === 0 ? 'M' : 'L'} ${point.screenX.toFixed(2)} ${point.screenY.toFixed(2)}`)
        .join(' ');
}

function buildRoutePaths(from: Vector3, to: Vector3, rotation: Rotation): RoutePaths | null {
    const samples = 88;
    const visibleSegments: ProjectedPoint[][] = [];
    let currentSegment: ProjectedPoint[] = [];

    for (let index = 0; index <= samples; index++) {
        const vector = slerpVector(from, to, index / samples);
        const projected = projectVector(vector, rotation);

        if (projected.z > 0.025) {
            currentSegment.push(projected);
        } else if (currentSegment.length > 1) {
            visibleSegments.push(currentSegment);
            currentSegment = [];
        } else {
            currentSegment = [];
        }
    }

    if (currentSegment.length > 1) {
        visibleSegments.push(currentSegment);
    }

    if (visibleSegments.length === 0) {
        return null;
    }

    const largestSegment = visibleSegments.reduce((largest, segment) => (
        segment.length > largest.length ? segment : largest
    ), visibleSegments[0]);

    return {
        pathD: visibleSegments.map(pathFromPoints).join(' '),
        planePathD: pathFromPoints(largestSegment),
    };
}

function createShader(
    gl: WebGLRenderingContext,
    type: number,
    source: string,
): WebGLShader | null {
    const shader = gl.createShader(type);
    if (!shader) return null;

    gl.shaderSource(shader, source);
    gl.compileShader(shader);

    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        console.warn('[LanguageSelectorGlobe] Shader compile failed:', gl.getShaderInfoLog(shader));
        gl.deleteShader(shader);
        return null;
    }

    return shader;
}

function createProgram(gl: WebGLRenderingContext): WebGLProgram | null {
    const vertexShader = createShader(gl, gl.VERTEX_SHADER, VERTEX_SHADER_SOURCE);
    const fragmentShader = createShader(gl, gl.FRAGMENT_SHADER, FRAGMENT_SHADER_SOURCE);
    if (!vertexShader || !fragmentShader) return null;

    const program = gl.createProgram();
    if (!program) return null;

    gl.attachShader(program, vertexShader);
    gl.attachShader(program, fragmentShader);
    gl.linkProgram(program);
    gl.deleteShader(vertexShader);
    gl.deleteShader(fragmentShader);

    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
        console.warn('[LanguageSelectorGlobe] Program link failed:', gl.getProgramInfoLog(program));
        gl.deleteProgram(program);
        return null;
    }

    return program;
}

interface EarthCanvasProps {
    rotation: Rotation;
    textureUrl: string;
}

const EarthCanvas: React.FC<EarthCanvasProps> = ({ rotation, textureUrl }) => {
    const canvasRef = useRef<HTMLCanvasElement>(null);
    const rotationRef = useRef(rotation);

    useEffect(() => {
        rotationRef.current = rotation;
    }, [rotation]);

    useEffect(() => {
        const canvas = canvasRef.current;
        if (!canvas) return;

        const gl = canvas.getContext('webgl', {
            alpha: true,
            antialias: true,
            depth: false,
            premultipliedAlpha: false,
        });

        if (!gl) {
            console.warn('[LanguageSelectorGlobe] WebGL is unavailable; showing marker layer only.');
            return;
        }

        const program = createProgram(gl);
        if (!program) return;

        const positionLocation = gl.getAttribLocation(program, 'a_position');
        const rotationLocation = gl.getUniformLocation(program, 'u_rotation');
        const timeLocation = gl.getUniformLocation(program, 'u_time');
        const mapLocation = gl.getUniformLocation(program, 'u_map');
        const hasTextureLocation = gl.getUniformLocation(program, 'u_hasTexture');
        const positionBuffer = gl.createBuffer();
        const texture = gl.createTexture();

        if (!positionBuffer || !texture) {
            gl.deleteProgram(program);
            return;
        }

        gl.bindBuffer(gl.ARRAY_BUFFER, positionBuffer);
        gl.bufferData(
            gl.ARRAY_BUFFER,
            new Float32Array([-1, -1, 1, -1, -1, 1, 1, 1]),
            gl.STATIC_DRAW,
        );

        gl.bindTexture(gl.TEXTURE_2D, texture);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texImage2D(
            gl.TEXTURE_2D,
            0,
            gl.RGBA,
            1,
            1,
            0,
            gl.RGBA,
            gl.UNSIGNED_BYTE,
            new Uint8Array([5, 30, 55, 255]),
        );

        let textureReady = false;
        let isDisposed = false;
        let rafId = 0;

        const image = new Image();
        image.decoding = 'async';
        image.onload = () => {
            if (isDisposed) return;
            gl.bindTexture(gl.TEXTURE_2D, texture);
            gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, 0);
            gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, image);
            textureReady = true;
        };
        image.src = textureUrl;

        const resize = () => {
            const rect = canvas.getBoundingClientRect();
            const pixelRatio = Math.min(window.devicePixelRatio || 1, 2);
            const width = Math.max(1, Math.round(rect.width * pixelRatio));
            const height = Math.max(1, Math.round(rect.height * pixelRatio));

            if (canvas.width !== width || canvas.height !== height) {
                canvas.width = width;
                canvas.height = height;
                gl.viewport(0, 0, width, height);
            }
        };

        const resizeObserver = new ResizeObserver(resize);
        resizeObserver.observe(canvas);
        resize();

        const render = (time: number) => {
            resize();

            gl.clearColor(0, 0, 0, 0);
            gl.clear(gl.COLOR_BUFFER_BIT);
            gl.useProgram(program);
            gl.bindBuffer(gl.ARRAY_BUFFER, positionBuffer);
            gl.enableVertexAttribArray(positionLocation);
            gl.vertexAttribPointer(positionLocation, 2, gl.FLOAT, false, 0, 0);
            gl.activeTexture(gl.TEXTURE0);
            gl.bindTexture(gl.TEXTURE_2D, texture);
            gl.uniform1i(mapLocation, 0);
            gl.uniform2f(rotationLocation, rotationRef.current.lng, rotationRef.current.lat);
            gl.uniform1f(timeLocation, time);
            gl.uniform1f(hasTextureLocation, textureReady ? 1 : 0);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

            rafId = requestAnimationFrame(render);
        };

        rafId = requestAnimationFrame(render);

        return () => {
            isDisposed = true;
            cancelAnimationFrame(rafId);
            resizeObserver.disconnect();
            gl.deleteTexture(texture);
            gl.deleteBuffer(positionBuffer);
            gl.deleteProgram(program);
        };
    }, [textureUrl]);

    return (
        <>
            <div className="maestro-earth-fallback" aria-hidden="true" />
            <canvas ref={canvasRef} className="maestro-earth-canvas" aria-hidden="true" />
        </>
    );
};

const LanguageSelectorGlobe: React.FC<LanguageSelectorGlobeProps> = ({
    nativeLangCode,
    targetLangCode,
    onSelectNative,
    onSelectTarget,
    onCancel,
    onInteract
}) => {
    const nativeLang = ALL_LANGUAGES.find(l => l.langCode === nativeLangCode) || null;
    const targetLang = ALL_LANGUAGES.find(l => l.langCode === targetLangCode) || null;
    const [hoveredLangCode, setHoveredLangCode] = useState<string | null>(null);
    const [isWheelOverlayActive, setIsWheelOverlayActive] = useState(false);
    const [isDragging, setIsDragging] = useState(false);
    const [rotation, setRotation] = useState<Rotation>(() => clampRotation({ lng: 0.45, lat: 0.18 }));

    const globeContainerRef = useRef<HTMLDivElement>(null);
    const wheelOverlayTimeoutRef = useRef<number | null>(null);
    const rotationRef = useRef(rotation);
    const isDraggingRef = useRef(false);
    const lastPointerRef = useRef({ x: 0, y: 0 });
    const velocityRef = useRef<Rotation>({ lng: 0, lat: 0 });
    const momentumFrameRef = useRef<number | null>(null);
    const focusFrameRef = useRef<number | null>(null);

    const globePoints = useMemo<GlobeLanguagePoint[]>(() => (
        ALL_LANGUAGES.map(lang => {
            const location = getLanguageGlobeLocation(lang);
            return {
                lang,
                location,
                vector: latLngToVector(location),
            };
        })
    ), []);

    const globePointByCode = useMemo(() => {
        const map = new Map<string, GlobeLanguagePoint>();
        globePoints.forEach(point => map.set(point.lang.langCode, point));
        return map;
    }, [globePoints]);

    const applyRotation = useCallback((updater: Rotation | ((previous: Rotation) => Rotation)) => {
        setRotation(previous => {
            const rawNext = typeof updater === 'function' ? updater(previous) : updater;
            const next = clampRotation(rawNext);
            rotationRef.current = next;
            return next;
        });
    }, []);

    const cancelMotion = useCallback(() => {
        if (momentumFrameRef.current !== null) {
            cancelAnimationFrame(momentumFrameRef.current);
            momentumFrameRef.current = null;
        }
        if (focusFrameRef.current !== null) {
            cancelAnimationFrame(focusFrameRef.current);
            focusFrameRef.current = null;
        }
    }, []);

    const handleInteraction = useCallback(() => {
        onInteract();
        setIsWheelOverlayActive(true);
        if (wheelOverlayTimeoutRef.current !== null) {
            window.clearTimeout(wheelOverlayTimeoutRef.current);
        }
        wheelOverlayTimeoutRef.current = window.setTimeout(() => {
            setIsWheelOverlayActive(false);
            wheelOverlayTimeoutRef.current = null;
        }, 1300);
    }, [onInteract]);

    const animateRotationToLocation = useCallback((location: GlobeLocation, durationMs = 680) => {
        cancelMotion();

        const start = rotationRef.current;
        const targetLngBase = normalizeAngle(-location.lng * DEG_TO_RAD);
        const target = {
            lng: start.lng + shortestAngleDelta(start.lng, targetLngBase),
            lat: clamp(location.lat * DEG_TO_RAD, -MAX_LAT_ROTATION, MAX_LAT_ROTATION),
        };
        const startedAt = performance.now();

        const step = (now: number) => {
            const progress = clamp01((now - startedAt) / durationMs);
            const eased = 1 - Math.pow(1 - progress, 3);

            applyRotation({
                lng: start.lng + (target.lng - start.lng) * eased,
                lat: start.lat + (target.lat - start.lat) * eased,
            });

            if (progress < 1) {
                focusFrameRef.current = requestAnimationFrame(step);
            } else {
                focusFrameRef.current = null;
            }
        };

        focusFrameRef.current = requestAnimationFrame(step);
    }, [applyRotation, cancelMotion]);

    const focusLanguage = useCallback((lang: LanguageDefinition, durationMs?: number) => {
        animateRotationToLocation(getLanguageGlobeLocation(lang), durationMs);
    }, [animateRotationToLocation]);

    useEffect(() => {
        rotationRef.current = rotation;
    }, [rotation]);

    useEffect(() => {
        const langToFocus = targetLang ?? nativeLang;
        if (!langToFocus) return;

        const timer = window.setTimeout(() => focusLanguage(langToFocus, 560), 120);
        return () => window.clearTimeout(timer);
    }, [focusLanguage, nativeLang, targetLang]);

    useEffect(() => {
        return () => {
            cancelMotion();
            if (wheelOverlayTimeoutRef.current !== null) {
                window.clearTimeout(wheelOverlayTimeoutRef.current);
            }
        };
    }, [cancelMotion]);

    useEffect(() => {
        if (!onCancel) return;

        const handler = (event: PointerEvent) => {
            if (globeContainerRef.current && !globeContainerRef.current.contains(event.target as Node)) {
                onCancel();
            }
        };

        const timer = window.setTimeout(() => document.addEventListener('pointerdown', handler, true), 100);
        return () => {
            window.clearTimeout(timer);
            document.removeEventListener('pointerdown', handler, true);
        };
    }, [onCancel]);

    const startMomentum = useCallback(() => {
        if (momentumFrameRef.current !== null) {
            cancelAnimationFrame(momentumFrameRef.current);
            momentumFrameRef.current = null;
        }

        const step = () => {
            const velocity = velocityRef.current;
            if (Math.abs(velocity.lng) < 0.00025 && Math.abs(velocity.lat) < 0.00025) {
                momentumFrameRef.current = null;
                return;
            }

            velocityRef.current = {
                lng: velocity.lng * 0.93,
                lat: velocity.lat * 0.93,
            };

            applyRotation(previous => ({
                lng: previous.lng + velocityRef.current.lng,
                lat: previous.lat + velocityRef.current.lat,
            }));

            momentumFrameRef.current = requestAnimationFrame(step);
        };

        momentumFrameRef.current = requestAnimationFrame(step);
    }, [applyRotation]);

    const handlePointerDown = useCallback((event: React.PointerEvent<HTMLDivElement>) => {
        if (!event.isPrimary) return;

        cancelMotion();
        handleInteraction();
        isDraggingRef.current = true;
        setIsDragging(true);
        lastPointerRef.current = { x: event.clientX, y: event.clientY };
        velocityRef.current = { lng: 0, lat: 0 };
        event.currentTarget.setPointerCapture(event.pointerId);
    }, [cancelMotion, handleInteraction]);

    const handlePointerMove = useCallback((event: React.PointerEvent<HTMLDivElement>) => {
        if (!isDraggingRef.current || !event.isPrimary) return;

        const dx = event.clientX - lastPointerRef.current.x;
        const dy = event.clientY - lastPointerRef.current.y;
        lastPointerRef.current = { x: event.clientX, y: event.clientY };

        const sensitivity = 0.0062;
        const nextVelocity = {
            lng: dx * sensitivity,
            lat: dy * sensitivity,
        };
        velocityRef.current = nextVelocity;

        applyRotation(previous => ({
            lng: previous.lng + nextVelocity.lng,
            lat: previous.lat + nextVelocity.lat,
        }));
    }, [applyRotation]);

    const finishPointerDrag = useCallback((event: React.PointerEvent<HTMLDivElement>) => {
        if (!isDraggingRef.current) return;

        isDraggingRef.current = false;
        setIsDragging(false);

        if (event.currentTarget.hasPointerCapture(event.pointerId)) {
            event.currentTarget.releasePointerCapture(event.pointerId);
        }

        if (Math.abs(velocityRef.current.lng) > 0.0005 || Math.abs(velocityRef.current.lat) > 0.0005) {
            startMomentum();
        }
    }, [startMomentum]);

    const handleWheel = useCallback((event: React.WheelEvent<HTMLDivElement>) => {
        event.preventDefault();
        event.stopPropagation();
        cancelMotion();
        handleInteraction();

        const dominantDelta = Math.abs(event.deltaX) > Math.abs(event.deltaY) ? event.deltaX : event.deltaY;
        const delta = dominantDelta >= 0 ? 0.16 : -0.16;

        applyRotation(previous => ({
            lng: previous.lng + delta,
            lat: previous.lat,
        }));
    }, [applyRotation, cancelMotion, handleInteraction]);

    const handleFlagClick = useCallback((lang: LanguageDefinition) => {
        handleInteraction();
        focusLanguage(lang);

        if (!nativeLang) {
            onSelectNative(lang.langCode);
        } else if (!targetLang && lang.langCode !== nativeLang.langCode) {
            onSelectTarget(lang.langCode);
        } else if (lang.langCode === nativeLang.langCode) {
            onSelectNative(null);
        } else if (lang.langCode === targetLang?.langCode) {
            onSelectTarget(null);
        } else {
            onSelectTarget(lang.langCode);
        }
    }, [focusLanguage, handleInteraction, nativeLang, onSelectNative, onSelectTarget, targetLang]);

    const handleScrollWheelSelect = useCallback((lang: LanguageDefinition, isNative: boolean) => {
        handleInteraction();
        focusLanguage(lang);

        if (isNative) {
            onSelectNative(lang.langCode);
        } else {
            onSelectTarget(lang.langCode);
        }
    }, [focusLanguage, handleInteraction, onSelectNative, onSelectTarget]);

    const projectedFlags = useMemo(() => (
        globePoints.map(point => {
            const projected = projectVector(point.vector, rotation);
            const frontness = smoothStep(-0.05, 0.88, projected.z);
            const centerFocus = clamp01((0.38 - projected.centerDistance) / 0.38);

            return {
                ...point,
                ...projected,
                visible: projected.z > -0.04,
                focusScore: frontness * centerFocus,
            };
        })
    ), [globePoints, rotation]);

    const focusedLangCode = useMemo(() => {
        const best = projectedFlags.reduce((winner, flag) => {
            if (!flag.visible || flag.focusScore < 0.34) return winner;
            if (!winner || flag.focusScore > winner.focusScore) return flag;
            return winner;
        }, null as (typeof projectedFlags)[number] | null);

        return best?.lang.langCode ?? null;
    }, [projectedFlags]);

    const sortedFlags = useMemo(() => (
        projectedFlags
            .filter(flag => flag.visible)
            .sort((a, b) => a.z - b.z)
    ), [projectedFlags]);

    const route = useMemo<RoutePaths | null>(() => {
        if (!nativeLang || !targetLang) return null;

        const nativePoint = globePointByCode.get(nativeLang.langCode);
        const targetPoint = globePointByCode.get(targetLang.langCode);
        if (!nativePoint || !targetPoint) return null;

        return buildRoutePaths(nativePoint.vector, targetPoint.vector, rotation);
    }, [globePointByCode, nativeLang, rotation, targetLang]);

    return (
        <div ref={globeContainerRef} className="w-full flex justify-center py-2">
            <style>{`
                @keyframes maestro-globe-route-dash {
                    from { stroke-dashoffset: 0; }
                    to { stroke-dashoffset: -64; }
                }

                @keyframes maestro-fallback-globe-spin {
                    from { background-position: 0% center; }
                    to { background-position: 200% center; }
                }

                .maestro-globe-shell {
                    position: relative;
                    container-type: inline-size;
                    overflow: visible;
                }

                .maestro-globe-window {
                    position: relative;
                    width: 100%;
                    aspect-ratio: 1 / 1;
                    overflow: visible;
                    border: 0;
                    background: transparent;
                    box-shadow: none;
                }

                .maestro-globe-stage {
                    position: absolute;
                    inset: 0;
                    cursor: grab;
                    touch-action: none;
                    user-select: none;
                    isolation: isolate;
                }

                .maestro-globe-stage.is-dragging {
                    cursor: grabbing;
                }

                .maestro-globe-space {
                    position: absolute;
                    inset: 0;
                    pointer-events: none;
                    background: transparent;
                }

                .maestro-earth-canvas {
                    position: absolute;
                    inset: 5%;
                    width: 90%;
                    height: 90%;
                    border-radius: 9999px;
                    pointer-events: none;
                    filter:
                        drop-shadow(0 0 18px rgba(84, 190, 255, 0.26))
                        drop-shadow(0 15px 26px rgba(0, 0, 0, 0.32));
                    z-index: 10;
                }

                .maestro-earth-fallback {
                    position: absolute;
                    inset: 5%;
                    border-radius: 9999px;
                    pointer-events: none;
                    z-index: 9;
                    background-image: url('/globe-image-background/Flag_map_of_the_world-1024.webp');
                    background-size: 200% auto;
                    background-position: center;
                    animation: maestro-fallback-globe-spin 120s linear infinite;
                    box-shadow:
                        inset 0 0 32px rgba(0,0,0,0.56),
                        0 0 18px rgba(84, 190, 255, 0.20);
                }

                .maestro-globe-vignette {
                    position: absolute;
                    inset: 0;
                    pointer-events: none;
                    z-index: 70;
                    display: none;
                }

                .maestro-globe-route-layer {
                    position: absolute;
                    inset: 0;
                    width: 100%;
                    height: 100%;
                    pointer-events: none;
                    z-index: 42;
                    overflow: visible;
                }

                .maestro-globe-route {
                    animation: maestro-globe-route-dash 18s linear infinite;
                    filter: drop-shadow(0 0 6px rgba(255,255,255,0.22));
                }

                .maestro-globe-plane {
                    filter: drop-shadow(0 1px 2px rgba(0,0,0,0.45));
                }

                .maestro-globe-flag-layer {
                    position: absolute;
                    inset: 0;
                    z-index: 60;
                    pointer-events: none;
                }

                .maestro-globe-flag-pin {
                    --pin-scale: 1;
                    position: absolute;
                    width: 3.4rem;
                    height: 3.4rem;
                    padding: 0;
                    border: 0;
                    background: transparent;
                    color: inherit;
                    pointer-events: auto;
                    transform: translate(-50%, -100%) scale(var(--pin-scale));
                    transform-origin: bottom center;
                    transition:
                        opacity 150ms ease-out,
                        filter 150ms ease-out,
                        transform 190ms cubic-bezier(.2,.8,.2,1);
                }

                .maestro-globe-flag-pin:focus-visible .maestro-globe-flag-card {
                    outline: 2px solid hsl(var(--profile-input-accent));
                    outline-offset: 2px;
                }

                .maestro-globe-pin-stem {
                    position: absolute;
                    left: 50%;
                    bottom: 0.22rem;
                    width: 2px;
                    height: 1.1rem;
                    transform: translateX(-50%);
                    background: linear-gradient(180deg, rgba(255,255,255,0.75), rgba(90, 210, 255, 0.34));
                    box-shadow: 0 0 8px rgba(120, 220, 255, 0.32);
                }

                .maestro-globe-pin-dot {
                    position: absolute;
                    left: 50%;
                    bottom: 0.02rem;
                    width: 0.38rem;
                    height: 0.38rem;
                    transform: translateX(-50%);
                    border-radius: 9999px;
                    background: rgba(212, 246, 255, 0.94);
                    box-shadow: 0 0 10px rgba(111, 220, 255, 0.65);
                }

                .maestro-globe-flag-card {
                    position: absolute;
                    left: 50%;
                    bottom: 1.16rem;
                    display: inline-flex;
                    align-items: center;
                    gap: 0.18rem;
                    transform: translateX(-50%);
                    min-width: 2.2rem;
                    min-height: 1.72rem;
                    padding: 0.18rem 0.28rem;
                    border: 1px solid rgba(255,255,255,0.50);
                    border-radius: 0.58rem;
                    background: rgba(6, 15, 28, 0.68);
                    backdrop-filter: blur(8px) saturate(1.25);
                    box-shadow:
                        0 7px 15px rgba(0,0,0,0.28),
                        inset 0 1px 0 rgba(255,255,255,0.22);
                }

                .maestro-globe-flag-glyph {
                    font-size: 1.26rem;
                    line-height: 1;
                    filter: drop-shadow(0 1px 1px rgba(0,0,0,0.30));
                }

                .maestro-globe-flag-code {
                    font-size: 0.48rem;
                    font-weight: 800;
                    line-height: 1;
                    color: rgba(255,255,255,0.86);
                    letter-spacing: 0.02em;
                }

                .maestro-globe-flag-label {
                    position: absolute;
                    left: 50%;
                    bottom: 2.98rem;
                    max-width: 8.5rem;
                    transform: translate(-50%, 0.25rem);
                    overflow: hidden;
                    text-overflow: ellipsis;
                    white-space: nowrap;
                    border-radius: 9999px;
                    padding: 0.16rem 0.46rem;
                    background: rgba(6, 15, 28, 0.78);
                    color: rgba(255,255,255,0.92);
                    font-size: 0.62rem;
                    font-weight: 700;
                    opacity: 0;
                    pointer-events: none;
                    transition: opacity 160ms ease-out, transform 160ms ease-out;
                    box-shadow: 0 5px 12px rgba(0,0,0,0.24);
                }

                .maestro-globe-flag-pin.is-expanded .maestro-globe-flag-label {
                    opacity: 1;
                    transform: translate(-50%, 0);
                }

                .maestro-globe-flag-pin.is-native .maestro-globe-flag-card {
                    border-color: hsl(var(--globe-native-accent));
                    box-shadow:
                        0 0 0 1px hsl(var(--globe-native-accent) / 0.25),
                        0 0 18px hsl(var(--globe-native-accent) / 0.42),
                        0 7px 15px rgba(0,0,0,0.28);
                }

                .maestro-globe-flag-pin.is-target .maestro-globe-flag-card {
                    border-color: hsl(var(--globe-target-accent));
                    box-shadow:
                        0 0 0 1px hsl(var(--globe-target-accent) / 0.25),
                        0 0 18px hsl(var(--globe-target-accent) / 0.42),
                        0 7px 15px rgba(0,0,0,0.28);
                }

                .maestro-globe-transcript-shell {
                    position: relative;
                    z-index: 90;
                    display: grid;
                    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
                    gap: 0.5rem;
                    width: calc(100% - 0.7rem);
                    height: clamp(9.5rem, 33cqw, 12.75rem);
                    margin: -0.625rem 0.35rem 0;
                    padding: 0.75rem;
                    border-radius: 0 0 0.5rem 0.5rem;
                    background: transparent;
                    overflow: hidden;
                    pointer-events: none;
                    text-shadow: none;
                    transition: opacity 180ms ease-out, transform 180ms ease-out, filter 180ms ease-out;
                }

                .maestro-globe-transcript-shell.is-idle {
                    opacity: 1;
                    transform: none;
                }

                .maestro-globe-transcript-shell:hover,
                .maestro-globe-transcript-shell:focus-within,
                .maestro-globe-transcript-shell.is-active {
                    opacity: 1;
                    transform: none;
                    filter: none;
                }

                .maestro-globe-transcript-column {
                    min-width: 0;
                    min-height: 0;
                    height: 100%;
                    overflow: hidden;
                    pointer-events: auto;
                }

                .maestro-globe-transcript-column.is-native {
                    border-right: 1px solid hsl(var(--attachment-svg-native-text) / 0.18);
                    padding-right: 0.45rem;
                }

                .maestro-globe-transcript-column.is-target {
                    padding-left: 0.45rem;
                }

                @media (max-width: 420px) {
                    .maestro-globe-transcript-shell {
                        width: 100%;
                        margin-inline: 0;
                        gap: 0.25rem;
                        padding-inline: 0.5rem;
                    }

                    .maestro-globe-transcript-column.is-native {
                        padding-right: 0.25rem;
                    }

                    .maestro-globe-transcript-column.is-target {
                        padding-left: 0.25rem;
                    }
                }

                @media (prefers-reduced-motion: reduce) {
                    .maestro-globe-route {
                        animation: none;
                    }
                    .maestro-earth-fallback {
                        animation: none;
                    }
                    .maestro-globe-flag-pin,
                    .maestro-globe-flag-label,
                    .maestro-globe-transcript-shell {
                        transition: none;
                    }
                }
            `}</style>

            <div className="maestro-globe-shell w-full max-w-[27rem]">
                <div className="maestro-globe-window">
                    <div
                        className={`maestro-globe-stage ${isDragging ? 'is-dragging' : ''}`}
                        onPointerDown={handlePointerDown}
                        onPointerMove={handlePointerMove}
                        onPointerUp={finishPointerDrag}
                        onPointerCancel={finishPointerDrag}
                        onWheel={handleWheel}
                        role="application"
                        aria-label="Interactive language globe"
                    >
                        <div className="maestro-globe-space" />
                        <EarthCanvas rotation={rotation} textureUrl={EARTH_TEXTURE_URL} />

                        {route && (
                            <svg viewBox="0 0 100 100" className="maestro-globe-route-layer" aria-hidden="true">
                                <path
                                    className="maestro-globe-route"
                                    d={route.pathD}
                                    stroke="rgba(255,255,255,0.46)"
                                    strokeWidth="0.74"
                                    strokeLinecap="round"
                                    strokeLinejoin="round"
                                    strokeDasharray="2.8 2.6"
                                    fill="none"
                                />
                                {route.planePathD && (
                                    <g key={route.planePathD} className="maestro-globe-plane">
                                        <path
                                            d="M -1.1 -0.36 L 1.28 0 L -1.1 0.36 L -0.55 0 L -1.1 -0.36 Z"
                                            fill="rgba(255,255,255,0.96)"
                                            stroke="rgba(8,20,34,0.46)"
                                            strokeWidth="0.12"
                                            transform="scale(2.15)"
                                        />
                                        <animateMotion
                                            dur="2.9s"
                                            repeatCount="indefinite"
                                            rotate="auto"
                                            path={route.planePathD}
                                        />
                                    </g>
                                )}
                            </svg>
                        )}

                        <div className="maestro-globe-flag-layer">
                            {sortedFlags.map(flag => {
                                const isNative = nativeLang?.langCode === flag.lang.langCode;
                                const isTarget = targetLang?.langCode === flag.lang.langCode;
                                const isFocused = focusedLangCode === flag.lang.langCode;
                                const isHovered = hoveredLangCode === flag.lang.langCode;
                                const isExpanded = isFocused || isHovered || isNative || isTarget;
                                const showShortCode = hasSharedFlag(flag.lang);
                                const depthOpacity = clamp(0.24 + smoothStep(-0.04, 1, flag.z) * 0.76, 0.18, 1);
                                const pinScale = isExpanded
                                    ? 1.22 + flag.focusScore * 0.32
                                    : 0.64 + smoothStep(0.02, 1, flag.z) * 0.20;
                                const pinStyle = {
                                    top: `${flag.screenY}%`,
                                    left: `${flag.screenX}%`,
                                    opacity: depthOpacity,
                                    zIndex: 20 + Math.round((flag.z + 1) * 45),
                                    filter: `saturate(${0.72 + smoothStep(-0.04, 1, flag.z) * 0.34})`,
                                    '--pin-scale': pinScale.toFixed(3),
                                } as React.CSSProperties;

                                return (
                                    <button
                                        key={flag.lang.langCode}
                                        className={[
                                            'maestro-globe-flag-pin',
                                            isExpanded ? 'is-expanded' : '',
                                            isNative ? 'is-native' : '',
                                            isTarget ? 'is-target' : '',
                                        ].filter(Boolean).join(' ')}
                                        style={pinStyle}
                                        onPointerDown={event => event.stopPropagation()}
                                        onClick={() => handleFlagClick(flag.lang)}
                                        onMouseEnter={() => setHoveredLangCode(flag.lang.langCode)}
                                        onMouseLeave={() => setHoveredLangCode(null)}
                                        onFocus={() => setHoveredLangCode(flag.lang.langCode)}
                                        onBlur={() => setHoveredLangCode(null)}
                                        title={flag.lang.displayName}
                                        aria-label={`Select ${flag.lang.displayName}`}
                                    >
                                        <span className="maestro-globe-pin-stem" />
                                        <span className="maestro-globe-pin-dot" />
                                        <span className="maestro-globe-flag-card">
                                            <span className="maestro-globe-flag-glyph">{flag.lang.flag}</span>
                                            {showShortCode && <span className="maestro-globe-flag-code">{flag.lang.shortCode}</span>}
                                        </span>
                                        <span className="maestro-globe-flag-label">{flag.lang.displayName}</span>
                                    </button>
                                );
                            })}
                        </div>

                        <div className="maestro-globe-vignette" />
                    </div>
                </div>

                <div
                    className={`maestro-globe-transcript-shell ${isWheelOverlayActive ? 'is-active' : 'is-idle'}`}
                    onPointerDown={event => event.stopPropagation()}
                    onTouchMove={event => event.stopPropagation()}
                    onWheel={event => event.stopPropagation()}
                >
                    <div className="maestro-globe-transcript-column is-native" data-globe-control="native">
                        <LanguageScrollWheel
                            languages={ALL_LANGUAGES}
                            selectedValue={nativeLang}
                            onSelect={(lang) => handleScrollWheelSelect(lang, true)}
                            onInteract={handleInteraction}
                            title="Native language"
                            variant="native"
                        />
                    </div>

                    <div className="maestro-globe-transcript-column is-target" data-globe-control="target">
                        <LanguageScrollWheel
                            languages={ALL_LANGUAGES.filter(l => l.langCode !== nativeLang?.langCode)}
                            selectedValue={targetLang}
                            onSelect={(lang) => handleScrollWheelSelect(lang, false)}
                            disabled={!nativeLang}
                            onInteract={handleInteraction}
                            title="Target language"
                            variant="target"
                        />
                    </div>
                </div>
            </div>
        </div>
    );
};

export default LanguageSelectorGlobe;
