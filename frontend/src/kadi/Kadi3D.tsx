import { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';
import { useTemaModu } from '../temaContext';
import { KadiSvg } from './KadiSvg';
import type { KadiDurum } from './kadiKanal';

/* S28b — Kadi'nin GERÇEK 3D bedeni (mockup "Kadi 3D" onaylı model): parlak plastik + cam vizör +
 * ışıklı halka gözler; tarayıcıda gerçek zamanlı render. Kafa imleci takip eder, durumlar poz
 * değiştirir (düşün: el çeneye · okey: kol kalkar · alarm: zıplar, gözler kırmızı — kullanıcı
 * seçimi: üzgün/alarm'da kol jesti YOK). Tema koyuysa sahne geceye döner. WebGL yoksa SVG bedene
 * düşer. Sekme gizliyken döngü durur. */

const MOR = new THREE.Color(0xa78bfa);
const KIRMIZI = new THREE.Color(0xff5d5d);
const MAVI = new THREE.Color(0x35c8ff);
const YESIL = new THREE.Color(0x2ee6a8);
const TEAL = new THREE.Color(0x14b8aa);

function isikDokusu(): THREE.CanvasTexture {
  const c = document.createElement('canvas');
  c.width = c.height = 128;
  const g = c.getContext('2d')!;
  const gr = g.createRadialGradient(64, 64, 4, 64, 64, 62);
  gr.addColorStop(0, 'rgba(80,200,255,1)');
  gr.addColorStop(0.35, 'rgba(80,200,255,.5)');
  gr.addColorStop(1, 'rgba(0,0,0,0)');
  g.fillStyle = gr;
  g.fillRect(0, 0, 128, 128);
  return new THREE.CanvasTexture(c);
}

/* Yumuşak ışıklı "oda" → parlak plastik/cam yansımaları (vizördeki pencere parlaması buradan). */
function ortamOlustur(renderer: THREE.WebGLRenderer): THREE.Texture {
  const pm = new THREE.PMREMGenerator(renderer);
  const sc = new THREE.Scene();
  sc.add(new THREE.Mesh(new THREE.BoxGeometry(60, 60, 60),
    new THREE.MeshBasicMaterial({ color: 0x50565e, side: THREE.BackSide })));
  const panel = (x: number, y: number, z: number, w: number, h: number, renk: number) => {
    const m = new THREE.Mesh(new THREE.PlaneGeometry(w, h), new THREE.MeshBasicMaterial({ color: renk }));
    m.position.set(x, y, z);
    m.lookAt(0, 0, 0);
    sc.add(m);
  };
  panel(0, 16, 0, 14, 14, 0xffffff);
  panel(-14, 5, 6, 9, 12, 0xe8eef4);
  panel(14, 3, -4, 7, 9, 0x9fb2c4);
  panel(0, 3, 16, 12, 7, 0xffffff);
  const doku = pm.fromScene(sc, 0.045).texture;
  pm.dispose();
  return doku;
}

type Rig = {
  robot: THREE.Group;
  kafa: THREE.Group;
  gozler: THREE.Group[];
  gozMat: THREE.MeshBasicMaterial;
  gozIsiklari: THREE.Sprite[];
  gobekIsik: THREE.MeshBasicMaterial;
  agiz: THREE.Mesh;
  antenler: THREE.Group[];
  kollar: { sol: THREE.Group; sag: THREE.Group };
  bacaklar: { sol: THREE.Group; sag: THREE.Group };
};

function robotKur(sahne: THREE.Scene): Rig {
  const beyaz = new THREE.MeshPhysicalMaterial({ color: 0xf4f6f8, roughness: 0.16, clearcoat: 1, clearcoatRoughness: 0.06 });
  const camSiyah = new THREE.MeshPhysicalMaterial({ color: 0x05080d, roughness: 0.04, clearcoat: 1, clearcoatRoughness: 0.03, envMapIntensity: 1.4 });
  const koyu = new THREE.MeshPhysicalMaterial({ color: 0x14181e, roughness: 0.35, metalness: 0.1, clearcoat: 0.6, envMapIntensity: 0.7 });
  const parilti = isikDokusu();

  const robot = new THREE.Group();
  sahne.add(robot);
  const rig: Rig = {
    robot, kafa: new THREE.Group(), gozler: [], gozIsiklari: [],
    gozMat: new THREE.MeshBasicMaterial({ color: MAVI.clone() }),
    gobekIsik: null as unknown as THREE.MeshBasicMaterial,
    agiz: null as unknown as THREE.Mesh, antenler: [],
    kollar: { sol: new THREE.Group(), sag: new THREE.Group() },
    bacaklar: { sol: new THREE.Group(), sag: new THREE.Group() },
  };

  const kafa = rig.kafa;
  kafa.position.y = 1.62;
  robot.add(kafa);
  const kafaKure = new THREE.Mesh(new THREE.SphereGeometry(1, 64, 48), beyaz);
  kafaKure.castShadow = true;
  kafa.add(kafaKure);
  kafa.add(new THREE.Mesh(new THREE.SphereGeometry(1.01, 64, 48,
    Math.PI / 2 - 0.85, 1.7, Math.PI / 2 - 0.6, 1.04), camSiyah));
  for (const s of [-1, 1]) {
    const kulak = new THREE.Mesh(new THREE.SphereGeometry(0.34, 32, 24), camSiyah);
    kulak.scale.set(0.22, 1, 1);
    kulak.position.set(s * 0.97, 0.02, 0);
    kafa.add(kulak);
    const anten = new THREE.Group();
    const cubuk = new THREE.Mesh(new THREE.CylinderGeometry(0.014, 0.02, 0.5, 12), koyu);
    cubuk.position.y = 0.25;
    const uc = new THREE.Mesh(new THREE.SphereGeometry(0.035, 16, 12), koyu);
    uc.position.y = 0.52;
    anten.add(cubuk, uc);
    anten.position.set(s * 0.4, 0.86, -0.05);
    anten.rotation.z = -s * 0.3;
    kafa.add(anten);
    rig.antenler.push(anten);
    const goz = new THREE.Group();
    const halka = new THREE.Mesh(new THREE.TorusGeometry(0.155, 0.045, 16, 40), rig.gozMat);
    const bebek = new THREE.Mesh(new THREE.CircleGeometry(0.105, 24), new THREE.MeshBasicMaterial({ color: 0x0a222e }));
    bebek.position.z = 0.012;
    const isik = new THREE.Sprite(new THREE.SpriteMaterial({
      map: parilti, blending: THREE.AdditiveBlending, depthWrite: false, opacity: 0.55 }));
    isik.scale.setScalar(0.8);
    goz.add(halka, bebek, isik);
    goz.position.set(s * 0.34, 0.08, 0.92);
    goz.rotation.y = s * 0.27;
    goz.rotation.x = -0.04;
    kafa.add(goz);
    rig.gozler.push(goz);
    rig.gozIsiklari.push(isik);
  }
  const agiz = new THREE.Mesh(new THREE.TorusGeometry(0.14, 0.017, 10, 28, Math.PI * 0.7), koyu);
  agiz.position.set(0, -0.5, 0.87);
  agiz.rotation.x = -0.38;
  agiz.rotation.z = -Math.PI / 2 - (Math.PI * 0.7) / 2;
  kafa.add(agiz);
  rig.agiz = agiz;

  const boyun = new THREE.Mesh(new THREE.CylinderGeometry(0.16, 0.2, 0.18, 24), koyu);
  boyun.position.y = 0.58;
  robot.add(boyun);
  const karin = new THREE.Mesh(new THREE.SphereGeometry(0.62, 48, 36), beyaz);
  karin.castShadow = true;
  karin.scale.set(0.88, 0.82, 0.8);
  karin.position.y = 0.5;
  robot.add(karin);
  const gobek = new THREE.Mesh(new THREE.SphereGeometry(0.1, 24, 16), camSiyah);
  gobek.scale.set(1, 1, 0.35);
  gobek.position.set(0, 0.52, 0.48);
  rig.gobekIsik = new THREE.MeshBasicMaterial({ color: TEAL.clone() });
  const gobekHalka = new THREE.Mesh(new THREE.TorusGeometry(0.12, 0.015, 10, 28), rig.gobekIsik);
  gobekHalka.position.set(0, 0.52, 0.5);
  robot.add(gobek, gobekHalka);

  for (const s of [-1, 1] as const) {
    const kol = new THREE.Group();
    kol.position.set(s * 0.58, 0.68, 0);
    kol.add(new THREE.Mesh(new THREE.SphereGeometry(0.14, 24, 18), koyu));
    const ust = new THREE.Mesh(new THREE.CapsuleGeometry(0.09, 0.28, 8, 16), beyaz);
    ust.castShadow = true;
    ust.position.set(s * 0.06, -0.23, 0);
    ust.rotation.z = s * 0.17;
    const el = new THREE.Mesh(new THREE.SphereGeometry(0.12, 20, 16), koyu);
    el.position.set(s * 0.12, -0.46, 0);
    kol.add(ust, el);
    robot.add(kol);
    rig.kollar[s === -1 ? 'sol' : 'sag'] = kol;

    const bacak = new THREE.Group();
    bacak.position.set(s * 0.24, 0.3, 0);
    const uyluk = new THREE.Mesh(new THREE.CapsuleGeometry(0.11, 0.1, 8, 16), beyaz);
    uyluk.castShadow = true;
    uyluk.position.y = -0.22;
    const ayak = new THREE.Mesh(new THREE.SphereGeometry(0.17, 24, 16), koyu);
    ayak.castShadow = true;
    ayak.scale.set(1, 0.45, 1.3);
    ayak.position.set(s * 0.01, -0.24, 0.06);
    bacak.add(uyluk, ayak);
    robot.add(bacak);
    rig.bacaklar[s === -1 ? 'sol' : 'sag'] = bacak;
  }
  return rig;
}

export function Kadi3D({ durum, mini }: { durum: KadiDurum; mini: boolean }) {
  const kanvasRef = useRef<HTMLCanvasElement>(null);
  const durumRef = useRef(durum);
  durumRef.current = durum;
  const { mod } = useTemaModu();
  const modRef = useRef(mod);
  modRef.current = mod;
  const [webglVar, setWebglVar] = useState(true);

  useEffect(() => {
    const kanvas = kanvasRef.current;
    if (!kanvas) return;
    let renderer: THREE.WebGLRenderer;
    try {
      renderer = new THREE.WebGLRenderer({ canvas: kanvas, antialias: true, alpha: true });
    } catch {
      setWebglVar(false);
      return;
    }
    renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
    renderer.setSize(kanvas.width, kanvas.height, false);
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;

    const sahne = new THREE.Scene();
    sahne.background = null; // sayfanın üstünde şeffaf yaşar
    sahne.environment = ortamOlustur(renderer);
    const kamera = new THREE.PerspectiveCamera(28, kanvas.width / kanvas.height, 0.1, 50);
    kamera.position.set(0, 1.5, 7.2);
    kamera.lookAt(0, 1.12, 0);

    const hemi = new THREE.HemisphereLight(0xffffff, 0x9aa8b6, 0.75);
    const anahtar = new THREE.DirectionalLight(0xffffff, 1.5);
    anahtar.position.set(2.4, 5, 3.2);
    anahtar.castShadow = true;
    anahtar.shadow.mapSize.set(512, 512);
    anahtar.shadow.radius = 5;
    anahtar.shadow.camera.left = -2; anahtar.shadow.camera.right = 2;
    anahtar.shadow.camera.top = 3; anahtar.shadow.camera.bottom = -1;
    const jant = new THREE.PointLight(0x14b8aa, 0, 12);
    jant.position.set(-2.2, 2.2, -2.4);
    const zemin = new THREE.Mesh(new THREE.PlaneGeometry(20, 20), new THREE.ShadowMaterial({ opacity: 0.22 }));
    zemin.rotation.x = -Math.PI / 2;
    zemin.receiveShadow = true;
    sahne.add(hemi, anahtar, jant, zemin);

    const rig = robotKur(sahne);

    let sonMod: string | null = null;
    const isiklaAyarla = () => {
      const gece = modRef.current === 'koyu';
      hemi.color.set(gece ? 0x334a5e : 0xffffff);
      hemi.groundColor.set(gece ? 0x0a141c : 0x9aa8b6);
      hemi.intensity = gece ? 0.5 : 0.75;
      anahtar.intensity = gece ? 0.9 : 1.5;
      jant.intensity = gece ? 6 : 0;
      (zemin.material as THREE.ShadowMaterial).opacity = gece ? 0.42 : 0.22;
      renderer.toneMappingExposure = gece ? 1.0 : 1.12;
      rig.gozIsiklari.forEach((s) => { s.material.opacity = gece ? 0.95 : 0.55; });
    };

    const fare = { x: 0, y: 0 };
    const fareIzle = (e: MouseEvent) => {
      const r = kanvas.getBoundingClientRect();
      fare.x = ((e.clientX - r.left) / Math.max(1, r.width) - 0.5) * 2;
      fare.y = ((e.clientY - r.top) / Math.max(1, r.height) - 0.5) * 2;
    };
    addEventListener('mousemove', fareIzle);

    const azHareket = matchMedia('(prefers-reduced-motion: reduce)').matches;
    let kirpma = 0;
    let sonKirp = performance.now() + 2000;
    let cerceve = 0;

    const kare = (t: number) => {
      cerceve = requestAnimationFrame(kare);
      if (modRef.current !== sonMod) { sonMod = modRef.current; isiklaAyarla(); }
      const sn = azHareket ? 0 : t / 1000;
      const d = durumRef.current;

      /* kollar — kullanıcı seçimi: jest yalnız selam/düşün/okey'de; üzgün/alarm nötr */
      let solZ = Math.sin(sn * 1.7) * 0.06, sagZ = -Math.sin(sn * 1.7 + 1) * 0.06;
      let sagX = 0;
      if (d === 'selam') solZ = -1.9 + Math.sin(sn * 10) * 0.3;
      if (d === 'okey') sagZ = -2.6;
      if (d === 'dusun') { sagZ = -2.75; sagX = -0.75; solZ = 0.22; }
      rig.kollar.sol.rotation.z += (solZ - rig.kollar.sol.rotation.z) * 0.14;
      rig.kollar.sag.rotation.z += (sagZ - rig.kollar.sag.rotation.z) * 0.14;
      rig.kollar.sag.rotation.x += (sagX - rig.kollar.sag.rotation.x) * 0.14;

      /* bacaklar: koşu/uçuş */
      const adim = d === 'kos' ? Math.sin(sn * 14) * 0.55 : d === 'ucus' ? 0.5 : 0;
      rig.bacaklar.sol.rotation.x += (adim - rig.bacaklar.sol.rotation.x) * 0.2;
      rig.bacaklar.sag.rotation.x += ((d === 'kos' ? -adim : adim) - rig.bacaklar.sag.rotation.x) * 0.2;

      /* kafa: imleç takibi + durum eğimleri */
      const kafaXek = d === 'dusun' ? -0.1 : d === 'uzgun' ? 0.32 : 0;
      const bakX = THREE.MathUtils.clamp(fare.y * 0.42, -0.35, 0.5) + kafaXek + Math.sin(sn * 0.9) * 0.03;
      const bakY = THREE.MathUtils.clamp(fare.x * 0.55, -0.6, 0.6) + Math.sin(sn * 0.7) * 0.04;
      rig.kafa.rotation.x += (bakX - rig.kafa.rotation.x) * 0.09;
      rig.kafa.rotation.y += (bakY - rig.kafa.rotation.y) * 0.09;
      rig.kafa.rotation.z += ((d === 'dusun' ? 0.17 : d === 'uzgun' ? -0.08 : 0) - rig.kafa.rotation.z) * 0.08;

      /* gözler: renk + kırpma */
      const renkHedef = d === 'dusun' ? MOR : d === 'alarm' ? KIRMIZI : d === 'okey' ? YESIL : MAVI;
      rig.gozMat.color.lerp(renkHedef, 0.12);
      rig.gobekIsik.color.lerp(d === 'alarm' ? KIRMIZI : TEAL, 0.12);
      if (t > sonKirp) { kirpma = 1; sonKirp = t + 2400 + Math.random() * 2400; }
      if (kirpma > 0) kirpma = Math.max(0, kirpma - 0.16);
      const gozTaban = d === 'uzgun' ? 0.45 : d === 'okey' ? 0.6 : 1;
      const gozOlcek = Math.min(gozTaban, 1 - kirpma * 0.95);
      for (const g of rig.gozler) g.scale.y += (gozOlcek - g.scale.y) * 0.5;

      /* gövde: nefes / zıplama / uçuş eğimi */
      const nefes = Math.sin(sn * 2.1) * 0.015;
      rig.robot.position.y = d === 'alarm' ? Math.abs(Math.sin(sn * 9)) * 0.3
        : d === 'ucus' ? 0.25 + Math.sin(sn * 3) * 0.05 : nefes;
      rig.robot.rotation.x += ((d === 'ucus' ? 0.3 : 0) - rig.robot.rotation.x) * 0.1;

      /* ağız + antenler */
      rig.agiz.scale.x += (((d === 'okey' || d === 'selam') ? 1.5 : d === 'uzgun' ? 0.001 : 1) - rig.agiz.scale.x) * 0.15;
      for (const a of rig.antenler) a.rotation.x = Math.sin(sn * (d === 'dusun' ? 9 : 2.2) + a.position.x) * 0.12;

      renderer.render(sahne, kamera);
    };
    cerceve = requestAnimationFrame(kare);

    const gorunurluk = () => {                 /* sekme gizliyken GPU boşa dönmesin */
      if (document.hidden) cancelAnimationFrame(cerceve);
      else cerceve = requestAnimationFrame(kare);
    };
    document.addEventListener('visibilitychange', gorunurluk);

    return () => {
      cancelAnimationFrame(cerceve);
      document.removeEventListener('visibilitychange', gorunurluk);
      removeEventListener('mousemove', fareIzle);
      renderer.dispose();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- sahne bir kez kurulur; durum/mod ref'ten okunur
  }, []);

  if (!webglVar) return <KadiSvg />;
  return (
    <canvas
      ref={kanvasRef}
      width={148}
      height={172}
      style={{ display: 'block', width: mini ? 46 : 111, height: mini ? 53 : 129 }}
    />
  );
}
