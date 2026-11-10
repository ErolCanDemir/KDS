/* S28b — Kadi'nin SVG bedeni: 3D (WebGL) kurulamayan ortamlar için yedek görünüm.
 * kadi.css'teki .kadi-* durum sınıfları bu beden için çalışmaya devam eder. */
export function KadiSvg() {
  return (
    <svg width="96" height="128" viewBox="0 0 120 160">
      <g className="kadi-parca kadi-anten"><circle className="kadi-tepe" cx="60" cy="12" r="5" fill="#0e9488" stroke="var(--kadi-koyu)" strokeWidth="2" /></g>
      <rect x="14" y="38" width="11" height="24" rx="5.5" fill="var(--kadi-koyu)" stroke="var(--kadi-koyu-cizgi)" strokeWidth="2" />
      <rect x="95" y="38" width="11" height="24" rx="5.5" fill="var(--kadi-koyu)" stroke="var(--kadi-koyu-cizgi)" strokeWidth="2" />
      <circle cx="60" cy="48" r="35" fill="var(--kadi-govde)" stroke="var(--kadi-cizgi)" strokeWidth="3" />
      <ellipse cx="45" cy="25" rx="14" ry="7" fill="var(--kadi-parlak)" opacity=".6" transform="rotate(-20 45 25)" />
      <rect x="29" y="30" width="62" height="42" rx="21" fill="var(--kadi-vizor)" />
      <g className="kadi-gozler kadi-parca">
        <g className="kadi-goz kadi-parca kadi-g1"><circle cx="46" cy="49" r="8" fill="#04222c" /><circle className="kadi-goz-ic" cx="46" cy="49" r="5.5" fill="var(--kadi-goz)" /><circle cx="44" cy="47" r="1.6" fill="#eafcff" /></g>
        <g className="kadi-goz kadi-parca kadi-g2"><circle cx="74" cy="49" r="8" fill="#04222c" /><circle className="kadi-goz-ic" cx="74" cy="49" r="5.5" fill="var(--kadi-goz)" /><circle cx="72" cy="47" r="1.6" fill="#eafcff" /></g>
      </g>
      <path className="kadi-agiz kadi-parca" d="M53 62 Q60 68 67 62" stroke="var(--kadi-goz)" strokeWidth="2.5" fill="none" strokeLinecap="round" />
      <rect x="50" y="82" width="20" height="9" rx="4" fill="var(--kadi-koyu)" stroke="var(--kadi-koyu-cizgi)" strokeWidth="2" />
      <g className="kadi-kol kadi-kol-sol kadi-parca"><circle cx="27" cy="97" r="8" fill="var(--kadi-koyu)" stroke="var(--kadi-koyu-cizgi)" strokeWidth="2" /><rect x="19" y="97" width="15" height="26" rx="7.5" fill="var(--kadi-govde)" stroke="var(--kadi-cizgi)" strokeWidth="3" /><circle cx="26.5" cy="128" r="6" fill="var(--kadi-koyu)" stroke="var(--kadi-koyu-cizgi)" strokeWidth="2" /></g>
      <g className="kadi-kol kadi-kol-sag kadi-parca"><circle cx="93" cy="97" r="8" fill="var(--kadi-koyu)" stroke="var(--kadi-koyu-cizgi)" strokeWidth="2" /><rect x="86" y="97" width="15" height="26" rx="7.5" fill="var(--kadi-govde)" stroke="var(--kadi-cizgi)" strokeWidth="3" /><circle cx="93.5" cy="128" r="6" fill="var(--kadi-koyu)" stroke="var(--kadi-koyu-cizgi)" strokeWidth="2" /></g>
      <ellipse cx="60" cy="112" rx="28" ry="26" fill="var(--kadi-govde)" stroke="var(--kadi-cizgi)" strokeWidth="3" />
      <ellipse cx="49" cy="97" rx="10" ry="5" fill="var(--kadi-parlak)" opacity=".55" transform="rotate(-15 49 97)" />
      <circle cx="46" cy="101" r="2" fill="var(--kadi-goz)" opacity=".85" />
      <circle cx="74" cy="101" r="2" fill="var(--kadi-goz)" opacity=".85" />
      <circle className="kadi-gogus" cx="60" cy="112" r="6" fill="#0e9488" />
      <g className="kadi-bacak kadi-bacak-sol kadi-parca"><rect x="41" y="132" width="13" height="14" rx="6" fill="var(--kadi-koyu)" stroke="var(--kadi-koyu-cizgi)" strokeWidth="2" /><ellipse cx="47" cy="149" rx="12" ry="6.5" fill="var(--kadi-govde)" stroke="var(--kadi-cizgi)" strokeWidth="3" /></g>
      <g className="kadi-bacak kadi-bacak-sag kadi-parca"><rect x="66" y="132" width="13" height="14" rx="6" fill="var(--kadi-koyu)" stroke="var(--kadi-koyu-cizgi)" strokeWidth="2" /><ellipse cx="73" cy="149" rx="12" ry="6.5" fill="var(--kadi-govde)" stroke="var(--kadi-cizgi)" strokeWidth="3" /></g>
      <path className="kadi-alev kadi-parca" d="M50 152 Q60 176 70 152 Q66 165 60 169 Q54 165 50 152 Z" fill="#ff9a3d" />
    </svg>
  );
}
