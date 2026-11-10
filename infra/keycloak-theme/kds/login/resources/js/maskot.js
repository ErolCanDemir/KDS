/* S26 — Nobetci Bot maskotlari (21 Agu 2026 mockup karari, varyant 3).
 * Davranis: acilista yukaridan duserler; gozleri ve govdeleriyle imleci izlerler;
 * kullanici adi odaklaninca o kutuya bakarlar; SIFREDE gozlerini obur tarafa kacirirlar
 * (yuz gorunur kalir — arkalarini donmezler); en kucuk uye arada gizlice bakar.
 * Login disindaki tema sayfalarinda (sifre sifirlama vb.) alanlar yoksa yalniz imlec takibi calisir.
 * prefers-reduced-motion: tum animasyonlar CSS tarafinda kapanir; JS takibi de devre disi kalir. */
(function () {
  'use strict';

  /* Tema script'leri HEAD'den yüklenir — DOM hazir olmadan #kdsTakim bulunamaz. */
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', kur);
  } else {
    kur();
  }

  function kur() {
  var takim = document.getElementById('kdsTakim');
  if (!takim) return;

  var azHareket = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  /* 4 uye: m2 teal (lider), m1/m3 gumus, m4 amber (kucuk) */
  for (var i = 1; i <= 4; i++) {
    var m = document.createElement('div');
    m.className = 'kds-maskot kds-m' + i + (azHareket ? '' : ' kds-dusen');
    m.style.animationDelay = (0.12 * i) + 's';
    m.innerHTML =
      '<div class="kds-salinim" style="--kst:' + (2.6 + i * 0.5).toFixed(2) + 's;--ksd:' + (i * 0.35).toFixed(2) + 's">' +
        '<div class="kds-anten"></div>' +
        '<div class="kds-govde">' +
          '<div class="kds-yuz">' +
            '<span class="kds-goz" style="--kkd:' + (i * 1.1 + 0.4).toFixed(2) + 's"><span class="kds-bebek"></span></span>' +
            '<span class="kds-goz" style="--kkd:' + (i * 1.1 + 0.4).toFixed(2) + 's"><span class="kds-bebek"></span></span>' +
          '</div>' +
        '</div>' +
      '</div>';
    takim.appendChild(m);
  }

  if (azHareket) return;

  var kullanici = document.getElementById('username');
  var sifre = document.getElementById('password');
  var hedef = null, odak = null, yazTimer = null;

  function ortasi(el) {
    var r = el.getBoundingClientRect();
    return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
  }

  function bak() {
    if (document.body.classList.contains('kds-kacir')) return;
    var nokta = (odak === 'k' && kullanici) ? ortasi(kullanici) : hedef;
    if (!nokta) return;
    var maskotlar = takim.querySelectorAll('.kds-maskot');
    for (var j = 0; j < maskotlar.length; j++) {
      var g = maskotlar[j].querySelector('.kds-govde');
      var r = g.getBoundingClientRect();
      var dx = nokta.x - (r.left + r.width / 2);
      var dy = nokta.y - (r.top + r.height / 2);
      var aci = Math.atan2(dy, dx);
      var mesafe = Math.min(1, Math.sqrt(dx * dx + dy * dy) / 260);
      var bebekler = g.querySelectorAll('.kds-bebek');
      for (var b = 0; b < bebekler.length; b++) {
        bebekler[b].style.transform =
          'translate(' + (Math.cos(aci) * 4 * mesafe) + 'px,' + (Math.sin(aci) * 4 * mesafe) + 'px)';
      }
      g.style.transform =
        'rotate(' + (Math.cos(aci) * 8 * mesafe) + 'deg) translateX(' + (Math.cos(aci) * 3 * mesafe) + 'px)';
    }
  }

  function inlineTemizle() {
    var eller = takim.querySelectorAll('.kds-govde, .kds-bebek');
    for (var j = 0; j < eller.length; j++) eller[j].style.transform = '';
  }

  document.addEventListener('mousemove', function (e) {
    hedef = { x: e.clientX, y: e.clientY };
    bak();
  });

  if (kullanici) {
    kullanici.addEventListener('focus', function () {
      odak = 'k';
      document.body.classList.remove('kds-kacir');
      bak();
    });
    kullanici.addEventListener('blur', function () { odak = null; });
  }

  if (sifre) {
    sifre.addEventListener('focus', function () {
      odak = 'p';
      inlineTemizle();                       /* CSS kacirma pozu inline transform'a takilmasin */
      document.body.classList.add('kds-kacir');
    });
    sifre.addEventListener('blur', function () {
      odak = null;
      document.body.classList.remove('kds-kacir');
      document.body.classList.remove('kds-yaziyor');
      inlineTemizle();
    });
    sifre.addEventListener('input', function () {
      document.body.classList.add('kds-yaziyor');
      clearTimeout(yazTimer);
      yazTimer = setTimeout(function () {
        document.body.classList.remove('kds-yaziyor');
      }, 900);
    });
  }
  }   /* kur() sonu */
})();
