/* S28b — Kadi login nobeti, GERCEK 3D beden (urundeki Kadi3D modelinin vanilya kopyasi).
 * three.min.js theme.properties'te bu dosyadan ONCE yuklenir. Login akisina DOKUNMAZ:
 * sag altta bekler, selam verir, kafasiyla imleci izler, sifrede BASINI CEVIRIR,
 * form gonderilince ortaya suzulup "Seni iceri almasini ilettim..." der.
 * WebGL yoksa Kadi hic gorunmez (login islevi etkilenmez). */
(function () {
  'use strict';

  function kur() {
    if (typeof THREE === 'undefined') return;
    var azHareket = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    var kap = document.createElement('div');
    kap.className = 'kadi-login';
    kap.setAttribute('aria-hidden', 'true');
    kap.innerHTML =
      '<div class="kadi-login-balon" id="kadiLoginBalon"><span class="kl-balon-ad">Kadi</span><span id="kadiLoginBalonMetin"></span></div>' +
      '<canvas id="kadiLoginKanvas" width="148" height="172" style="display:block;width:111px;height:129px"></canvas>';
    document.body.appendChild(kap);

    var balon = document.getElementById('kadiLoginBalon');
    var balonTimer = null;
    function soyle(metin, sureMs) {
      document.getElementById('kadiLoginBalonMetin').textContent = metin;
      balon.classList.add('kl-gorunur');
      clearTimeout(balonTimer);
      if (sureMs) balonTimer = setTimeout(function () { balon.classList.remove('kl-gorunur'); }, sureMs);
    }

    /* ---- sahne ---- */
    var kanvas = document.getElementById('kadiLoginKanvas');
    var renderer;
    try {
      renderer = new THREE.WebGLRenderer({ canvas: kanvas, antialias: true, alpha: true });
    } catch (hata) {
      kap.removeChild(kanvas);
      return; /* WebGL yok: yalniz balonlu selam kalir */
    }
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.setSize(148, 172, false);
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.12;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;

    var sahne = new THREE.Scene();
    var pm = new THREE.PMREMGenerator(renderer);
    var oda = new THREE.Scene();
    oda.add(new THREE.Mesh(new THREE.BoxGeometry(60, 60, 60),
      new THREE.MeshBasicMaterial({ color: 0x50565e, side: THREE.BackSide })));
    function panel(x, y, z, w, h, renk) {
      var m = new THREE.Mesh(new THREE.PlaneGeometry(w, h), new THREE.MeshBasicMaterial({ color: renk }));
      m.position.set(x, y, z); m.lookAt(0, 0, 0); oda.add(m);
    }
    panel(0, 16, 0, 14, 14, 0xffffff);
    panel(-14, 5, 6, 9, 12, 0xe8eef4);
    panel(14, 3, -4, 7, 9, 0x9fb2c4);
    panel(0, 3, 16, 12, 7, 0xffffff);
    sahne.environment = pm.fromScene(oda, 0.045).texture;
    pm.dispose();

    var kamera = new THREE.PerspectiveCamera(28, 148 / 172, 0.1, 50);
    kamera.position.set(0, 1.5, 7.2);
    kamera.lookAt(0, 1.12, 0);
    sahne.add(new THREE.HemisphereLight(0xffffff, 0x9aa8b6, 0.75));
    var anahtar = new THREE.DirectionalLight(0xffffff, 1.5);
    anahtar.position.set(2.4, 5, 3.2);
    anahtar.castShadow = true;
    anahtar.shadow.mapSize.set(512, 512);
    anahtar.shadow.radius = 5;
    anahtar.shadow.camera.left = -2; anahtar.shadow.camera.right = 2;
    anahtar.shadow.camera.top = 3; anahtar.shadow.camera.bottom = -1;
    sahne.add(anahtar);
    var zemin = new THREE.Mesh(new THREE.PlaneGeometry(20, 20), new THREE.ShadowMaterial({ opacity: 0.22 }));
    zemin.rotation.x = -Math.PI / 2;
    zemin.receiveShadow = true;
    sahne.add(zemin);

    /* ---- robot (urundeki Kadi3D ile ayni model) ---- */
    var beyaz = new THREE.MeshPhysicalMaterial({ color: 0xf4f6f8, roughness: .16, clearcoat: 1, clearcoatRoughness: .06 });
    var camSiyah = new THREE.MeshPhysicalMaterial({ color: 0x05080d, roughness: .04, clearcoat: 1, clearcoatRoughness: .03, envMapIntensity: 1.4 });
    var koyu = new THREE.MeshPhysicalMaterial({ color: 0x14181e, roughness: .35, metalness: .1, clearcoat: .6, envMapIntensity: .7 });
    var gozMat = new THREE.MeshBasicMaterial({ color: 0x35c8ff });

    var robot = new THREE.Group(); sahne.add(robot);
    var kafa = new THREE.Group(); kafa.position.y = 1.62; robot.add(kafa);
    var kafaKure = new THREE.Mesh(new THREE.SphereGeometry(1, 64, 48), beyaz);
    kafaKure.castShadow = true; kafa.add(kafaKure);
    kafa.add(new THREE.Mesh(new THREE.SphereGeometry(1.01, 64, 48,
      Math.PI / 2 - .85, 1.7, Math.PI / 2 - .6, 1.04), camSiyah));
    var gozler = [], antenler = [];
    [-1, 1].forEach(function (s) {
      var kulak = new THREE.Mesh(new THREE.SphereGeometry(.34, 32, 24), camSiyah);
      kulak.scale.set(.22, 1, 1); kulak.position.set(s * .97, .02, 0); kafa.add(kulak);
      var anten = new THREE.Group();
      var cubuk = new THREE.Mesh(new THREE.CylinderGeometry(.014, .02, .5, 12), koyu);
      cubuk.position.y = .25;
      var uc = new THREE.Mesh(new THREE.SphereGeometry(.035, 16, 12), koyu);
      uc.position.y = .52;
      anten.add(cubuk, uc);
      anten.position.set(s * .4, .86, -.05);
      anten.rotation.z = -s * .3;
      kafa.add(anten); antenler.push(anten);
      var goz = new THREE.Group();
      goz.add(new THREE.Mesh(new THREE.TorusGeometry(.155, .045, 16, 40), gozMat));
      var bebek = new THREE.Mesh(new THREE.CircleGeometry(.105, 24), new THREE.MeshBasicMaterial({ color: 0x0a222e }));
      bebek.position.z = .012;
      goz.add(bebek);
      goz.position.set(s * .34, .08, .92);
      goz.rotation.y = s * .27; goz.rotation.x = -.04;
      kafa.add(goz); gozler.push(goz);
    });
    var agiz = new THREE.Mesh(new THREE.TorusGeometry(.14, .017, 10, 28, Math.PI * .7), koyu);
    agiz.position.set(0, -.5, .87);
    agiz.rotation.x = -.38;
    agiz.rotation.z = -Math.PI / 2 - (Math.PI * .7) / 2;
    kafa.add(agiz);
    var boyun = new THREE.Mesh(new THREE.CylinderGeometry(.16, .2, .18, 24), koyu);
    boyun.position.y = .58; robot.add(boyun);
    var karin = new THREE.Mesh(new THREE.SphereGeometry(.62, 48, 36), beyaz);
    karin.castShadow = true; karin.scale.set(.88, .82, .8); karin.position.y = .5; robot.add(karin);
    var gobek = new THREE.Mesh(new THREE.SphereGeometry(.1, 24, 16), camSiyah);
    gobek.scale.set(1, 1, .35); gobek.position.set(0, .52, .48);
    var gobekHalka = new THREE.Mesh(new THREE.TorusGeometry(.12, .015, 10, 28),
      new THREE.MeshBasicMaterial({ color: 0x14b8aa }));
    gobekHalka.position.set(0, .52, .5);
    robot.add(gobek, gobekHalka);
    var kollar = {};
    [-1, 1].forEach(function (s) {
      var kol = new THREE.Group();
      kol.position.set(s * .58, .68, 0);
      kol.add(new THREE.Mesh(new THREE.SphereGeometry(.14, 24, 18), koyu));
      var ust = new THREE.Mesh(new THREE.CapsuleGeometry(.09, .28, 8, 16), beyaz);
      ust.castShadow = true; ust.position.set(s * .06, -.23, 0); ust.rotation.z = s * .17;
      var el = new THREE.Mesh(new THREE.SphereGeometry(.12, 20, 16), koyu);
      el.position.set(s * .12, -.46, 0);
      kol.add(ust, el);
      robot.add(kol);
      kollar[s === -1 ? 'sol' : 'sag'] = kol;
      var bacak = new THREE.Mesh(new THREE.CapsuleGeometry(.11, .1, 8, 16), beyaz);
      bacak.castShadow = true; bacak.position.set(s * .24, .08, 0);
      var ayak = new THREE.Mesh(new THREE.SphereGeometry(.17, 24, 16), koyu);
      ayak.castShadow = true; ayak.scale.set(1, .45, 1.3); ayak.position.set(s * .25, .06, .06);
      robot.add(bacak, ayak);
    });

    /* ---- davranis ---- */
    var fare = { x: 0, y: 0 };
    document.addEventListener('mousemove', function (e) {
      var r = kanvas.getBoundingClientRect();
      fare.x = ((e.clientX - r.left) / Math.max(1, r.width) - .5) * 2;
      fare.y = ((e.clientY - r.top) / Math.max(1, r.height) - .5) * 2;
    });
    var selamBitis = performance.now() + 2600;   /* acilista el sallar */
    var kirpma = 0, sonKirp = performance.now() + 2000;

    function kare(t) {
      requestAnimationFrame(kare);
      var sn = azHareket ? 0 : t / 1000;
      var kacir = document.body.classList.contains('kds-kacir');
      var selamda = t < selamBitis;

      var solZ = selamda ? -1.9 + Math.sin(sn * 10) * .3 : Math.sin(sn * 1.7) * .06;
      var sagZ = -Math.sin(sn * 1.7 + 1) * .06;
      kollar.sol.rotation.z += (solZ - kollar.sol.rotation.z) * .14;
      kollar.sag.rotation.z += (sagZ - kollar.sag.rotation.z) * .14;

      /* sifrede basini cevirir (Nobetci Botlar gibi o da bakmaz) */
      var bakX = kacir ? .05 : Math.max(-.35, Math.min(.5, fare.y * .42)) + Math.sin(sn * .9) * .03;
      var bakY = kacir ? -2.3 : Math.max(-.6, Math.min(.6, fare.x * .55)) + Math.sin(sn * .7) * .04;
      kafa.rotation.x += (bakX - kafa.rotation.x) * .09;
      kafa.rotation.y += (bakY - kafa.rotation.y) * (kacir ? .12 : .09);

      if (t > sonKirp) { kirpma = 1; sonKirp = t + 2400 + Math.random() * 2400; }
      if (kirpma > 0) kirpma = Math.max(0, kirpma - .16);
      var gozOlcek = Math.min(1, 1 - kirpma * .95);
      for (var i = 0; i < gozler.length; i++) gozler[i].scale.y += (gozOlcek - gozler[i].scale.y) * .5;

      robot.position.y = Math.sin(sn * 2.1) * .015;
      agiz.scale.x += ((selamda ? 1.5 : 1) - agiz.scale.x) * .15;
      for (var a = 0; a < antenler.length; a++)
        antenler[a].rotation.x = Math.sin(sn * 2.2 + antenler[a].position.x) * .12;

      renderer.render(sahne, kamera);
    }
    requestAnimationFrame(kare);

    setTimeout(function () { soyle('Merhaba, ben Kadi! Giriş yap, seni içeri aldırayım.', 4600); }, 900);

    var form = document.getElementById('kc-form-login');
    if (form) {
      form.addEventListener('submit', function () {
        kap.classList.add('kadi-login-merkez');
        selamBitis = performance.now() + 2200;   /* ortada el sallayarak ugurlar */
        soyle('Seni içeri almasını ilettim…', 0);
      });
    }
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', kur);
  else kur();
})();
